# BigFatFile Architecture (v2)

Companion documents:

- `Architecture.Platform.md`: How BFF is used to build apps, SDKs, packages, conventions.
- `Architecture.API.md`: the one public API: conventions, authentication, scopes, endpoints.
- `Architecture.Modules.md`: how the open core and licensed modules plug in, the module list.
- `Architecture.Storage.md`: content chunking, warehouses, garbage collection, encryption.
- `Architecture.Transfer.md`: upload sessions, downloads, sends, transport settings.
- `Architecture.Materialisation.md`: how clients show remote drives as local folders.
- `Architecture.Observability.md`: OTEL instrumentation; what we measure and where the data goes.
- `Architecture.Deployment.md`: images, compose files, NAS hosts, installers, updates, signing.

## 1. Goals

- One codebase, three deployments:
    - the public cloud at bigfatfile.com;
    - dedicated single-tenant instances for specific clients;
    - self-hosted installs by anyone.
- The desktop client must feel like a local disk:
    - files open at the speed of a local disk;
    - an office keeps working while its internet connection is down.
- Open-source core, licensed modules for the rest:
    - the core is AGPL and is a complete product for a team;
    - licensed modules that plug into the core (`Architecture.Modules.md`):
        - features that an org's IT, compliance or finance people ask for;
        - features needed to host BFF as a service for other parties.
- One UI stack:
    - the web app, the desktop app and the Replica admin page are Svelte 5
      applications that share design, components and translations.
- A platform, not only a product:
    - other applications are built on BFF through the same API and SDKs our own
      clients use;
    - our clients are the first third-party apps: they use only the public API
      (`Architecture.Platform.md`).

## 2. Components

Project is a monorepo with sub-projects.

Core topology: Origin Server <-> Replica Server <=> Apps (Clients).

- Origin Server, "the Origin" from here on:
    - the one authoritative copy of a tenant's data.
    - runs in the cloud, on a dedicated instance, or self-hosted.
- Replica Server, "the Replica" from here on:
    - an optional full mirror on the office network.
    - talks to the Origin upstream and serves Clients downstream at LAN speed, online or offline.
- Apps (aka "clients"):
    - the desktop app, the web app, the CLI and any other third-party Apps.
    - they talk to the Replica when one is reachable, otherwise to the Origin;
    - the protocol is the same either way: the one public API, whose chunk-level
      endpoints the SDKs use on their behalf (`Architecture.API.md`).
- The Replica is itself an App:
    - a first-party, chunk-level App with the tenant-wide mirror scope, released with
      the Origin;
    - it also serves the same API downstream to the office;
    - there are no private APIs; the platform is the Origin alone.

### 2.1 Applications

#### **BigFatFile.Server** (.NET), aka the Origin Server:

- The same image runs/serves:
    - the API, the web UI and the real-time hub;
    - background jobs;
    - commands: `migrate`, `backup` and `restore`.
- Runtime:
    - ASP.NET Core;
    - output Docker images for linux/amd64 and linux/arm64;
    - [chiseled](https://devblogs.microsoft.com/dotnet/announcing-dotnet-chiseled-containers/#chiseled-images) ASP.NET image, runing as non-root;
    - health-check at `/health` (no Google-biased endpoints like "healthz").
- Database:
    - PostgreSQL;
    - Dapper (minimalist ORM, uses explicit SQL);
        - I avoid hidden behaviors (like EF Core generators);
        - some system components still use EF Core internally (auth) and we keep them as they are;
    - Badgie.Migrator:
        - runs schema migrations at startup with an advisory lock so concurrent nodes
          don't race.
- Tenant isolation:
    - application-level scoping;
    - PostgreSQL row-level security ([section 9](#9-multi-tenancy-and-permissions)).
- Jobs and queues:
    - all done via Hangfire with PostgreSQL storage ([section 10](#10-jobs)).
- Storage:
    - hash-chunked files written into warehouses ([section 4](#4-content-storage)).
- Web UI (served from .NET):
    - Svelte 5 SPA built with Vite;
    - Tailwind CSS, shadcn-svelte, Lucide icons;
    - Paraglide (i18n);
    - TanStack Query (server state), TanStack Virtual, TanStack HotKeys.
- Authentication:
    - the Origin is the identity provider for the whole system ([section 8](#8-authentication)).

#### **BigFatFile.Desktop** (.NET):

- A sync engine with OS integration;
    - shows the whole remote drive immediately, however large;
    - fetches file content on first open into a managed local cache (LRU);
- Uses materialisation model (avoid filesystem drivers where possible):
    - real folder entries that are hollow until opened ([Architecture.Materialisation.md](Architecture.Materialisation.md)).
- Platforms:
    - Linux: FUSE with a local write-back cache.
    - Windows: Cloud Files API (similar to OneDrive), Windows 10 1903+, NTFS, packaged as MSIX;
    - macOS: File Provider framework, with a thin Swift extension over the same engine;
- Local state:
    - SQLite (metadata, versions, pin state, local journal, change-feed cursor, access history).
- Local API:
    - small HTTP server on loopback interface, random port, per-install security token;
    - serves the settings UI and exposes status, settings, pause and resume, selective
      sync, sign-in and conflicts;
    - serves live status over a WebSocket;
    - the CLI uses this API to talk to a running instance.
- Settings window:
    - Photino.NET window hosting the platform's webview consuming the local API;
    - the UI is a Svelte 5 app (shared stack, runs in browser during dev).
- Native per platform:
    - tray icon and menu, notifications, auto-start, Explorer context-menu actions.

#### **BigFatFile.Replica** (.NET), aka the Replica Server:

- Server app acting as:
    - full mirror of its tenant;
    - the on-prem backup;
- Runs in an office on the local network (NAS, mini PC, Windows service):
    - LAN clients use it instead of the Origin;
    - it answers at LAN speed and forwards writes to the Origin in the background;
    - keeps working offline and reconciles with the Origin when the link returns ([section 6](#6-real-time-sync));
- Local state:
    - SQLite (not PostgreSQL);
    - chunks in a `local` warehouse;
- Admin page:
    - Svelte 5 app (shared UI stack);
    - served on the local network.

#### **BigFatFile.Cli**:

- Client-side commands built over the client SDK.
- Scripted uploads and downloads, folder sync, admin tasks, CI usage;

### 2.2 Libraries

- **BigFatFile.Contracts**:
    - everything the Origin, the Replica and every client must agree on exactly:
        - request and response types, error codes, real-time message types;
        - the chunker and its parameters, the hash function and the manifest format;
        - ID generation, name normalisation, path limits, the conflict-copy naming rule.
    - no dependencies beyond the base classes;
    - covered by test vectors (JSON fixtures) that the TypeScript implementation also runs.
- **BigFatFile.Server.Abstractions**:
    - the seam between the open core and the licensed modules;
    - `IBffModule` (module registration, endpoints, embedded migrations), `IFeatureGate`;
    - interfaces the core consumes: `IChunkStore`, `IWarehouseRegistry`, `IChangeNotifier`,
      `IAuthProvider`, `IMalwareScanner`, `IPlanPolicy`, `IGroupDirectory`;
    - domain events modules subscribe to; access to the tenant context, audit writer,
      job registration and encrypted per-tenant settings;
    - changes rarely, because the private repository compiles against it.
- **BigFatFile.Client** (the SDK, published to NuGet):
    - typed API client over Contracts;
    - authentication flows and a pluggable secure token store;
    - the transfer engine: chunk, dedup check, parallel upload with resume, verified
      download, presigned direct-to-storage when offered, bandwidth limits, retries;
    - real-time subscription with reconnect; change-feed paging;
    - endpoint discovery and failover between Origin and Replica.
- **BigFatFile.Sync**:
    - local state, journal, reconciliation, conflict policy, cache and prefetch policy,
      delta detection, the local API;
    - platform-neutral; used by Desktop, Replica and Cli.
- **Licensed modules** (private repository):
    - the modules listed in `Architecture.Modules.md`, one project each;
    - compile against Server.Abstractions; loaded at startup; enabled by a licence key.

### 2.3 Web workspace (`web/`)

- Web-dedicated workspace:
    - ui codes: web app, desktop app UI, Replica admin UI;
    - common code: design-system tokens, shadcn-svelte components, Paraglide message catalogues etc.;
- Packages:
    - `@bigfatfile/client`:
        - types generated from the OpenAPI spec;
        - the upload engine ported from `Client`;
        - the chunker and the hash function compiled into WebAssembly.
    - `@bigfatfile/ui`:
        - reusable UI components: file browser, uploader, sharing configurator etc.

### 2.4 Dependency graph

```
BigFatFile.Contracts
        ^      ^
        |      +-- BigFatFile.Server.Abstractions
        |                        ^
        |                        +-- BigFatFile.Server
        |                        +-- licensed modules (private)
        |
BigFatFile.Client
        ^
        |
BigFatFile.Sync
        ^
        |
        +-- BigFatFile.Desktop
        +-- BigFatFile.Replica
        +-- BigFatFile.Cli
```

**Notes:**

- Domain rules (permissions, quotas, plans), database tables and anything that
  references ASP.NET or the database stay in Server and are never shared with clients.

## 3. Data model

### 3.1 File tree

- The `nodes` table keeps a row for each file or folder:
    - tree hierarchy is enforced via the `parent_id` column (an adjacency list);
    - listing a folder is one index lookup;
    - moving a folder is one row update plus a check that the destination is not inside
      the moved folder;
    - paths are not stored; instead they're computed with a recursive CTE (a query that
      walks parent links) when needed.
- Key columns:
    - `id`: UUIDv7, generated by whoever creates the node, including offline clients;
    - `tenant_id`, `drive_id`, `parent_id`, `kind`, `name`, `name_key`, `version`,
      `manifest_id`, `size`, `deleted_at`.
- `name_key`:
    - the name casefolded and Unicode-normalised (NFC);
    - sibling names are unique on `name_key`, because Windows and macOS are
      case-insensitive and clients must detect collisions offline.
- Limits:
    - 64 levels deep, 4,096 characters per full path.
- Delete moves a node under the drive's trash root; restore moves it back.
- Metrics:
    - each drive keeps a `used_bytes` counter, updated in the same transaction as the change,
      so quota checks never have to sum-up a whole ubtree.
- Optional(later) full path cache per-node:
    - a nullable `path_cache` column;
    - filled lazily;
    - has to be cleared for a subtree when a folder moves.

### 3.2 File versioning

- Every node has an integer `version` that increases on each change.
- Every write carries the version the client last saw (`If-Match`); the Origin rejects
  the write if the version moved on.
- This detects every conflict case:
    - two web users;
    - a desktop client against the Origin;
    - two clients through one Replica;
    - a Replica sync-ing against the Origin.

### 3.3 Change log

- Every mutation of the tree inserts a row into `changes` in the same transaction:
    - per-tenant sequence number;
    - change type (created, updated, renamed, moved, deleted);
    - node;
    - parent;
    - new version;
    - actor (user and client ID);
    - time.
- The sequence number comes from a counter row on the tenant:
    - incremented inside the transaction, so sequence order == commit order:
    - a plain database sequence would not guarantee that.
- Clients read it with `GET /changes?since=<seq>`.
- Retention is configurable (default 90 days):
    - a client with an older cursor does a full tree resync.

### 3.4 Audit log

- Separate from the change log;
- Stores:
    - who did what;
    - sign-ins;
    - share-link visits;
    - downloads and permission changes.
- Retention depends on the plan (on cloud version).
- Uses a partitioned table, so old partitions can be dropped cheaply.

## 4. Content storage

- Drive files are stored as content-defined chunks, deduplicated within a tenant,
  described by manifests and kept in warehouses (`local` or `s3`).
- See [Architecture.Storage.md](Architecture.Storage.md) for details.

## 5. Transfer protocol

- How bytes move through the file-level and the chunk-level endpoints, the tenant-wide
  scope mirroring Apps use, how sends move, and why throughput depends on parallel TCP
  connections.
- See [Architecture.Transfer.md](Architecture.Transfer.md) for details.

## 6. Real-time sync

### 6.1 Notifications

- The change log is the authoritative truth;
- Clients connect to a SignalR hub and receive `ChangesAvailable(tenantId, latestSeq)`;
    - the push channel only says that something changed;
    - the payload carries no data.
- A client reads the log from its cursor on certain events:
    - when it receives that message;
    - after a reconnect;
    - after waking from sleep;
    - on a timer.
- Change storage/detection:
    - For one server process, committed changes go on an in-process bus.
    - In cluster mode each node listens on a PostgreSQL `NOTIFY` channel, so changes
      committed on one node reaches clients connected to another.

### 6.2 Desktop client loop

1. Sync changes from Origin or Replica:
    - read the change log from the stored cursor until empty;
    - apply entries that touch directories the client has materialised;
    - discard all other entries (not affecting client);
    - persist the cursor after each page.
2. Listen for available changes:
    - connect to the SignalR hub;
    - on each change notification, repeat step 1 (debounced).
3. Sync local changes:
    - receive local changes from the platform adapter;
    - wait until a changed file is closed and quiet;
    - chunk the file;
    - upload only the missing chunks;
    - complete upload with `If-Match` over the manifest;
    - mark the file as being "in sync".

- Lazy metadata, content caching and eviction, change detection per platform and the
  first-sign-in prefetch are in `Architecture.Materialisation.md`.

### 6.3 Conflicts

- No merges and no silent overwrites.
- A write rejected because the version moved on becomes a conflict copy:
    - named `<name> (conflict copy, <device>).<ext>`, next to the original;
    - both appear everywhere;
    - the user resolves afterwards from Explorer or the web UI.
- A remote delete of a file with un-synced local edits keeps the local file as a
  conflict copy.
- Edits by the same client are recognised by the actor's client ID and are not conflicts.
- Later feature:
    - "someone else is editing this file" notifications sent over the same SignalR hub.

## 7. Replica

- Registration:
    - a tenant administrator registers the Replica at the Origin; it receives a key pair;
    - the Origin publishes the Replica's LAN address and public key to the tenant's
      clients;
    - clients prefer the Replica when reachable and fall back to the Origin;
    - switching endpoints triggers a version-based reconcile of the client's tree, not a
      cursor replay, because the two servers have different sequence spaces.
- Full mirror and active backup:
    - a complete copy of the tenant's tree, permissions, change log and every referenced
      chunk, for the whole tenant or selected drives;
    - metadata in SQLite, chunks in a `local` warehouse on the host's disks;
    - capacity planning: the tenant's deduplicated size plus retained versions;
    - applies the whole change feed without filtering; fetches missing chunks in the
      background, prioritising chunks LAN clients ask for;
    - a verification job compares every manifest against the chunks on disk, re-fetches
      anything missing, and reports status to the Origin ("Replica complete as of
      <time>" in the web UI).
- Backup semantics:
    - version retention is configured independently of the Origin's plan and may be
      longer;
    - restores run from the Replica's admin page: a folder to a point in time, or a
      single version;
    - a restore is a metadata operation plus local reads, because manifests are
      immutable and chunks are content-addressed.
- Online:
    - reads are served from the mirror; a chunk not yet mirrored is fetched from the
      Origin on demand;
    - writes are accepted at LAN speed, acknowledged, and queued in a durable journal (outbox).
- Offline:
    - reads and writes continue; LAN clients keep receiving change events;
    - all LAN writes pass through one Replica, so the LAN side has one linear history and
      conflicts between two LAN users are detected immediately.
- Reconnect:
    - pull the Origin's changes since the stored cursor and emit them as local events;
    - replay the journal in order, each operation carrying its base version;
    - on a rejected write, upload the LAN version as a conflict copy and keep the
      Origin's version as primary;
    - chunks are uploaded before the manifest that references them, so a half-uploaded
      file is never visible.
- Authentication on the LAN:
    - tokens are issued by the Origin; the Replica verifies them offline with the Origin's
      public signing key and enforces permissions from its local copy;
    - LAN tokens get a longer grace period so an outage does not sign everyone out.

## 8. Authentication

- The server is the identity provider; no separate auth service.
- Accounts:
    - email and password with verification; external providers (Google, Microsoft,
      GitHub) through standard OAuth;
    - the operator can point the Origin at one OpenID Connect provider (Keycloak,
      Authentik, Entra ID);
    - per-tenant identity providers, SAML, SCIM, group sync and LDAP are licensed modules
      behind `IAuthProvider` (`Architecture.Modules.md`).
- Libraries:
    - ASP.NET Core Identity for accounts (password hashing, email confirmation, lockout,
      two-factor, passkeys);
    - OpenIddict as the OpenID Connect server (authorization code with PKCE, device code,
      refresh tokens, client credentials, JWKS, introspection, revocation);
    - Microsoft's authentication handlers for upstream providers;
    - `Duende.IdentityModel.OidcClient` in desktop clients and the CLI;
    - MailKit for email.
- Flows:
    - web UI: session cookie, same origin, no tokens in the browser;
    - desktop, Replica, CLI: authorization code with PKCE through the system browser and a
      loopback redirect, or device code for headless installs;
    - refresh tokens live in the platform's secure store (DPAPI, keychain, protected file).
- Tokens:
    - JWTs signed with an asymmetric key (ES256); public keys published at the JWKS
      endpoint;
    - this is what lets the Replica verify tokens offline; symmetric signing is not used.
- On first sign-in the Origin finds or creates the user, links the external account, and
  provisions tenant membership from invitations or the tenant's domain rules.
- Data Protection keys (cookies, anti-forgery) are stored in the database so every node
  and container restart shares them.
- Stores:
    - Identity and OpenIddict are store-based and ship EF Core implementations;
    - one small EF Core `DbContext` holds only their tables and is used at runtime by
      those two libraries; all BigFatFile tables stay on Dapper;
    - the SQL for those tables is generated once from EF and committed as Badgie
      migrations; EF's own migrations never run;
    - row-level security does not apply to these tables; users are global and tenant
      membership is ours.

## 9. Multi-tenancy and permissions

- A tenant is a company or an individual, with users, groups, drives, a warehouse and
  settings (plan, auth providers). A user may belong to several tenants.
- Application-level scoping (first layer):
    - every tenant-owned table carries `tenant_id`; every query filters on it;
    - an isolation test suite calls every endpoint with another tenant's identifiers and
      expects a 404.
- Row-level security (second layer):
    - one policy per tenant-owned table:
        - `tenant_id = current_setting('app.tenant_id', true)::uuid`
    - request handling opens a transaction and first runs:
        - `SET LOCAL app.tenant_id = '<id>'`
    - the setting ends with the transaction, so a pooled connection never carries a
      tenant into the next request;
    - the application connects as a role that does not own the tables, because table
      owners bypass row-level security;
    - cross-tenant work (garbage collection, billing, administration) runs under a
      separate role with `BYPASSRLS`;
    - predicates stay this simple so the planner keeps using the `(tenant_id, …)`
      indexes.
- Permissions:
    - rules on nodes grant a role (viewer, editor, owner) to a user or a group;
    - the effective permission is the nearest rule walking up the tree;
    - external people get access through share links and guest memberships, never
      through anonymous write.
- Sends (transfers to recipients outside the system): nodes under a hidden send root
  with an expiry, a download link and an optional password.
- Plans: the open core has one unlimited plan; the billing module enforces storage,
  bandwidth, retention and feature limits through `IPlanPolicy`.

## 10. Jobs

- Hangfire with PostgreSQL storage is the single scheduling mechanism:
    - recurring jobs: send expiry, garbage collection, retention;
    - one-off work: malware scan after upload, thumbnails, notification emails, webhooks;
    - retries with backoff; dashboard under the admin area; jobs receive identifiers, not
      objects.
- In cluster mode Hangfire coordinates across nodes through its PostgreSQL storage.
- The one advisory lock outside Hangfire guards schema migrations at startup.

## 11. Deployment

- How the Origin, the Replica, the desktop client and the CLI are packaged, installed
  and updated on each platform, and how images are signed.
- See [Architecture.Deployment.md](Architecture.Deployment.md) for details.

## 12. Open core and licensed modules

- Where the line between the open core (AGPL) and the licensed modules is, how a module
  plugs into the Origin, and the list of modules.
- See [Architecture.Modules.md](Architecture.Modules.md) for details.

## 13. Observability

- How the Origin, the Replica and the clients report traces, metrics and logs with
  OpenTelemetry, what BFF measures, and where the data goes.
- See [Architecture.Observability.md](Architecture.Observability.md) for details.

## 14. API

- The one public API of the Origin, `/api/v1`: conventions, authentication, scopes and
  the endpoint list; the chunk-level endpoints are part of it under an "advanced" tag.
- See [Architecture.API.md](Architecture.API.md) for details.

## 15. Platform

- How other applications are built on BFF: Apps, the primitives they use, API
  conventions, SDKs and UI packages, and the developer loop.
- See [Architecture.Platform.md](Architecture.Platform.md) for details.
