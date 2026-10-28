# BigFatFile Architecture (v2)

Status: design, not yet implemented.

Companion documents:

- `Architecture.Filesystems.md`: how the desktop client shows the remote drive as a
  local folder on Windows, macOS and Linux; content caching; platform adapters.
- `Architecture.Deployment.md`: images, compose files, NAS hosts, installers, updates,
  signing.
- `Architecture.Modules.md`: the open core and licensed modules line, how modules plug
  in, the module list.

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
    - the web app, the desktop app and the replica admin page are Svelte 5
      applications that share design, components and translations.

## 2. Components

Project is a monorepo with sub-projects.

### 2.1 Applications

#### **BigFatFile.Server** (.NET):

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
    - the server is the identity provider for the whole system ([section 8](#8-authentication)).

#### **BigFatFile.Desktop** (.NET):

- A sync engine with OS integration;
    - shows the whole remote drive immediately, however large;
    - fetches file content on first open into a managed local cache (LRU);
- Uses materialisation model (avoid filesystem drivers where possible):
    - real folder entries that are hollow until opened (`Architecture.Filesystems.md`).
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

#### **BigFatFile.Replica** (.NET):

- Server app acting as:
    - full mirror of its tenant;
    - the on-prem backup;
- Runs in an office on the local network (NAS, mini PC, Windows service):
    - LAN clients use it instead of the origin;
    - it answers at LAN speed and forwards writes to the origin in the background;
    - keeps working offline and reconciles with the origin when the link returns ([section 6](#6-real-time-sync));
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
    - everything the server and every client must agree on exactly:
        - request and response types, error codes, real-time message types;
        - the chunk size, the hash function and the manifest format;
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
    - endpoint discovery and failover between origin and replica.
- **BigFatFile.Sync**:
    - local state, journal, reconciliation, conflict policy, cache and prefetch policy,
      delta detection, the local API;
    - platform-neutral; used by Desktop, Replica and Cli.
- **Licensed modules** (private repository):
    - the modules listed in `Architecture.Modules.md`, one project each;
    - compile against Server.Abstractions; loaded at startup; enabled by a licence key.

### 2.3 Web workspace (`web/`)

- Web-dedicated workspace:
    - ui codes: web app, desktop app UI, replica admin UI;
    - common code: design-system tokens, shadcn-svelte components, Paraglide message catalogues etc.;
- Packages:
    - `@bigfatfile/client`:
        - types generated from the OpenAPI spec;
        - the upload engine ported from `Client`;
        - the hash function compiled into WebAssembly.
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
- Every write carries the version the client last saw (`If-Match`); the server rejects
  the write if the version moved on.
- This detects every conflict case:
    - two web users;
    - a desktop client against the server;
    - two clients through one replica;
    - a replica sync-ing against the origin.

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

### 4.1 Chunks and manifests

- Drive files are stored as chunks;
- Plain sends (transfers) are stored as whole objects ([section 5](#5-transfer-protocol)).
- A chunk is a fixed-size block of the file at a fixed offset:
    - 4 MiB, the last chunk is usually shorter;
    - identified by the BLAKE3 hash of its bytes;
    - stored once per tenant within a warehouse, however many files or versions of that
      tenant reference it.
- Deduplication is scoped to the tenant, never across tenants:
    - cross-tenant sharing would let anyone test whether a given file exists somewhere
      in the system by asking "do you already have this chunk?";
    - it would also make "your data is deleted" untrue while another tenant still
      references a chunk.
- A file version is a manifest: the ordered list of chunk hashes and sizes, the total
  size, and the hash of the whole file.

```json
{
    "size": 104857600,
    "checksum": "blake3:…",
    "chunks": [
        { "hash": "blake3:…", "size": 4194304 },
        { "hash": "blake3:…", "size": 2097152 }
    ]
}
```

- Fixed offsets capture the delta for in-place edits (databases, mail stores, design
  files, VM images), which is the common case for large working files:
    - a save uploads only the chunks whose bytes changed;
    - inserting bytes in the middle of a file shifts every later chunk; content-defined
      chunking (FastCDC) handles that case and can be added later per file type, since
      the manifest already allows variable sizes.
- The manifest has two copies:
    - database: the working copy; used for range-to-chunk mapping, the missing-chunks
      check, garbage collection, version history and the `If-Match` check;
    - warehouse (`manifests/<tenant>/<id>`): the recovery copy, written once at commit
      and never read in normal operation; carries tenant, node ID, version, size,
      timestamps and the path at commit time; read only by `bff export`, disaster
      recovery and integrity audits.
- A daily job writes a tree snapshot per tenant into the warehouse
  (`snapshots/<tenant>/<date>.json`), so manifests plus the latest snapshot rebuild
  names, folders and contents without the database.
- Commit order: chunks, then the warehouse manifest, then the database transaction. The
  database never references what the warehouse lacks. The database is authoritative
  when the two disagree, except in recovery mode.
- What chunking gives: upload of only the changed parts, instant upload of a file the
  tenant already has, resumable and parallel transfers, integrity checks per chunk,
  cheap versioning (a version costs only its new chunks), and a replica that moves only
  changed chunks.

### 4.2 Warehouses

- A warehouse is a named storage target: an ID, a type and the settings for that type.
- Types in the open core:
    - `local`: a directory;
    - `s3`: endpoint, bucket, region, credentials, whether presigned URLs may be issued;
      works with Cloudflare R2, Backblaze B2, Wasabi, Garage, RustFS, MinIO.
- Two sources, merged by `IWarehouseRegistry`:
    - server configuration, owned by the operator (open core):
        - a default warehouse is mandatory (`BFF_DEFAULT_WAREHOUSE` names it); more are
          optional;
        - definitions live in a mounted file (`/config/warehouses.json`); credentials are
          referenced from environment variables or Docker secrets, never written in the
          file;
    - the database, defined by tenant administrators in the web UI, credentials
      encrypted at rest (licensed module `storage.tenant-warehouses`); this is how a
      tenant brings its own bucket.
- Every tenant has a `warehouse_id`; new tenants get the default unless an administrator
  assigns another.
- Chunk identity is `(warehouse_id, tenant_id, hash)`: deduplication, the missing-chunks
  check and garbage collection run within one tenant of one warehouse.
- Moving a tenant between warehouses is a job: copy the referenced chunks, switch the
  tenant, let garbage collection clean the old location.
- Startup checks:
    - probe every configured warehouse (write and read a test object);
    - verify every warehouse referenced by a tenant exists;
    - either failure stops the server.
- Cluster mode requires every warehouse to be shared storage (S3 or a shared
  filesystem); a `local` warehouse on one node's disk fails the check.
- `local` implementation:
    - `chunks/<tenant>/ab/cd/<hash>` with two levels of fan-out; `objects/<tenant>/<id>`
      for sends; `manifests/` and `snapshots/` as in section 4.1;
    - write to a temporary file in the same directory, fsync, rename into place;
    - any local filesystem works; network filesystems work too (nothing locks).
- `s3` implementation:
    - the AWS SDK with a configurable endpoint;
    - presigned URLs let browsers and clients move bytes directly to and from storage.

### 4.3 Garbage collection

- A chunk is garbage when no retained version of the tenant references it.
- Mark and sweep per tenant: list every referenced hash, list the tenant's chunks,
  delete what is stored but not referenced.
- A pruned version's warehouse manifest is deleted the same way; an expired send's
  object is deleted when the send expires.
- Two safeguards for chunks in flight:
    - an upload session pins its chunks until it completes or expires;
    - the sweep never deletes a chunk written less than 24 hours ago.
- Runs as a recurring Hangfire job.

### 4.4 Encryption

- Client-side (end-to-end) encryption is not part of v2.
- It would remove deduplication within a tenant, server-side malware scanning, and the
  replica's ability to serve plain files on the LAN.
- It stays a research item; the chunk model does not prevent adding it later for
  specific drives.

## 5. Transfer protocol

### 5.1 Drive files

- Uploads use an upload session shaped like S3 multipart:
    1. `POST /uploads` with the manifest; the server returns the chunks the tenant does
       not have and, on S3 warehouses, a presigned URL for each.
    2. The client uploads the missing chunks in parallel, one request each, to the server
       (`PUT /uploads/{id}/chunks/{hash}`) or directly to storage; the server verifies
       each chunk's hash on receipt.
    3. `POST /uploads/{id}/complete` attaches the manifest to the node, with `If-Match` on
       the node's version.
- Resume is automatic: a restarted client repeats step 1 and is told what is still
  missing. Sessions expire after a configurable time.
- Downloads by our own clients (desktop, CLI, replica) assemble on the client:
    - `GET /nodes/{id}/manifest` returns the manifest and, on S3 warehouses, a presigned
      URL per chunk with a short expiry;
    - the client fetches the chunks it lacks in parallel (from the replica, the origin or
      storage), verifies each hash, and writes them at their offsets;
    - the server moves no bytes on S3 warehouses; corruption is caught per chunk; resume
      is per chunk.
- Downloads in the browser:
    - the web app assembles in the browser where a streaming download is possible
      (service worker, or the File System Access API), and otherwise falls back to the
      server;
    - previews and playback (PDF, video) always go through the server:
      `GET /nodes/{id}/content` with range support, the server maps the range to chunks
      and streams only what is requested.
- Share links to drive files are served by the server (or the replica on the LAN) as a
  streamed, assembled file; multi-file links as a streamed zip.
- The web uploader slices the file at 4 MiB and hashes with BLAKE3 in WebAssembly; the
  desktop client does the same natively.

### 5.2 Sends

- A send (a transfer to recipients outside the system) is stored as whole-file objects,
  not chunks:
    - a send is immutable, has no versions and needs no delta;
    - its traffic is mostly downloads by external recipients on any browser or phone,
      which must be a plain download.
- Upload: presigned multipart straight to storage on S3 warehouses (`PUT` to the server
  on `local` warehouses); resume through the multipart part list.
- Download: a presigned URL straight from storage on S3 warehouses; the server touches no
  bytes. On `local` warehouses the server streams the object. Multi-file sends download
  as a streamed zip.
- Expiry deletes the objects.

### 5.3 Transport

- HTTP/3 is enabled on the server and the reverse proxy; no custom transport.

## 6. Real-time sync

### 6.1 Notification

- The change log is the truth; the push channel only says that something changed.
- Clients connect to a SignalR hub and receive `ChangesAvailable(tenantId, latestSeq)`;
  the payload carries no data.
- A client reads the log from its cursor when it receives that message, after a
  reconnect, after waking from sleep, and on a timer.
- Inside one server process, committed changes go on an in-process bus.
- In cluster mode each node listens on a PostgreSQL `NOTIFY` channel, so a change
  committed on one node reaches clients connected to another.

### 6.2 Desktop client loop

1. Read the change log from the stored cursor until empty; apply entries that touch
   directories the client has materialised; discard the rest; persist the cursor after
   each page.
2. Connect to the hub; on each notification, repeat step 1 (debounced).
3. Receive local changes from the platform adapter; wait until a changed file is closed
   and quiet; chunk it; upload only the missing chunks; complete with `If-Match`; mark
   the file in sync.

- Lazy metadata, content caching and eviction, change detection per platform and the
  first-sign-in prefetch are in `Architecture.Filesystems.md`.

### 6.3 Conflicts

- No merges and no silent overwrites.
- A write rejected because the version moved on becomes a conflict copy:
    - named `<name> (conflict copy, <device>).<ext>`, next to the original;
    - both appear everywhere; the user resolves afterwards from Explorer or the web UI.
- A remote delete of a file with unsynced local edits keeps the local file as a conflict
  copy.
- Edits by the same client are recognised by the actor's client ID and are not conflicts.
- Later feature: "someone else is editing this file" notices over the same hub.

## 7. Replica

- Registration:
    - a tenant administrator registers the replica at the origin; it receives a key pair;
    - the origin publishes the replica's LAN address and public key to the tenant's
      clients;
    - clients prefer the replica when reachable and fall back to the origin;
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
      anything missing, and reports status to the origin ("replica complete as of
      <time>" in the web UI).
- Backup semantics:
    - version retention is configured independently of the origin's plan and may be
      longer;
    - restores run from the replica's admin page: a folder to a point in time, or a
      single version;
    - a restore is a metadata operation plus local reads, because manifests are
      immutable and chunks are content-addressed.
- Online:
    - reads are served from the mirror; a chunk not yet mirrored is fetched from the
      origin on demand;
    - writes are accepted at LAN speed, acknowledged, and queued in a durable journal (outbox).
- Offline:
    - reads and writes continue; LAN clients keep receiving change events;
    - all LAN writes pass through one replica, so the LAN side has one linear history and
      conflicts between two LAN users are detected immediately.
- Reconnect:
    - pull the origin's changes since the stored cursor and emit them as local events;
    - replay the journal in order, each operation carrying its base version;
    - on a rejected write, upload the LAN version as a conflict copy and keep the
      origin's version as primary;
    - chunks are uploaded before the manifest that references them, so a half-uploaded
      file is never visible.
- Authentication on the LAN:
    - tokens are issued by the origin; the replica verifies them offline with the origin's
      public signing key and enforces permissions from its local copy;
    - LAN tokens get a longer grace period so an outage does not sign everyone out.

## 8. Authentication

- The server is the identity provider; no separate auth service.
- Accounts:
    - email and password with verification; external providers (Google, Microsoft,
      GitHub) through standard OAuth;
    - the operator can point the server at one OpenID Connect provider (Keycloak,
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
    - desktop, replica, CLI: authorization code with PKCE through the system browser and a
      loopback redirect, or device code for headless installs;
    - refresh tokens live in the platform's secure store (DPAPI, keychain, protected file).
- Tokens:
    - JWTs signed with an asymmetric key (ES256); public keys published at the JWKS
      endpoint;
    - this is what lets the replica verify tokens offline; symmetric signing is not used.
- On first sign-in the server finds or creates the user, links the external account, and
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
- Row-level security (second layer): - one policy per tenant-owned table: `tenant_id = current_setting('app.tenant_id',
true)::uuid`; - request handling opens a transaction and runs `SET LOCAL app.tenant_id = '<id>'`
  first; the setting dies with the transaction, so a pooled connection never carries
  a tenant into the next request; - the application connects as a role that does not own the tables (owners bypass
  RLS); - cross-tenant work (garbage collection, billing, administration) runs under a
  separate role with `BYPASSRLS`; - predicates stay this simple so the planner keeps using the `(tenant_id, …)`
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

- Server and replica are deployed with Docker Compose; the desktop client and the CLI
  through each platform's installer or package manager.
- One release tag builds every artifact with one version number; every .NET artifact is
  self-contained.
- Images, compose files, NAS hosts, installers, updates and signing are in
  `Architecture.Deployment.md`.

## 12. Open core and licensed modules

- The open core (AGPL) is a complete product for a team.
- Licensed modules add what an organisation's IT, compliance or finance people ask for,
  and what is needed to host BFF as a service for other people.
- A module is one assembly against Server.Abstractions, loaded from `/app/modules/` and
  enabled by a licence key checked through `IFeatureGate`.
- The line, the loading mechanism and the module list are in `Architecture.Modules.md`.

## 13. Open items

- Project naming: `Desktop` for the application, `Client` for the SDK, `Replica` for the
  LAN server are the working names.
- An online compose configurator (a form that generates `docker-compose.yml` and `.env`).
- Numbers to confirm with a prototype: the 4 MiB chunk size, change-log retention
  default, garbage-collection grace period.
