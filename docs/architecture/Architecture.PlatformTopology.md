# BigFatFile Architecture: Platform topology

The platform as a whole: the topology, the projects and the repository, how Apps and
licensed modules plug in, the SDKs, and the developer experience.

## 1. Core topology

Core topology: Origin Server <-> Replica Server <=> Apps (Clients).

- **Origin**:
    - the one authoritative copy of a tenant's data;
    - runs in the cloud, on a dedicated instance, or self-hosted;
    - the platform is the Origin alone.
- **Replica**:
    - optional (not required) and acts as a full mirror of a tenant on the LAN;
    - constantly syncs with the Origin upstream;
    - replicates the Origin's API downstream to listening Apps;
    - serves downstream Apps at LAN speed (even when uplink is down);
    - is actually itself an App (chunk-level, tenant-scoped);
    - see more details in:
        - `Architecture.API.md` section 4;
        - `Architecture.FileTransfer.md` section 4.
- **Apps**:
    - Web, Desktop.?, Cli, Replica and any third-party App;
    - they talk to the Replica when one is reachable, otherwise to the Origin;
    - the protocol is the same either way:
        - the one public API, whose chunk-level endpoints the SDKs use on their behalf (`Architecture.API.md`).
    - a BFF App has:
        - level:
            - file-level Apps use the `files` endpoints (high-level);
            - chunk-level Apps use the `transfer` endpoints directly (low-level);
        - scope:
            - user-scoped Apps act on what their user may see;
            - tenant-scoped Apps require `tenant.mirror` scope and see all the tenant's data.
    - provided Apps (Web, Desktop.? etc.) are actually BFF Apps too:
        - they use only the published API and SDKs (acceptance test);
        - no private endpoints, no internal shortcuts;
        - anything our UI can do, a 3rd party App can also do;
        - this means:
            - the API provides completeness from scratch;
            - the SDKs are battle-tested by our own Apps before others use them.

## 2. Projects

The BFF codebase is a monorepo with multiple projects (binaries and libraries).
Licensed modules are (by exception) kept in a separate repository.

### Binaries

#### **BigFatFile.Server** (.NET), aka the Origin:

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
    - schema migrations:
        - `core_<number>_<name>.sql` files embedded in the Server assembly;
        - run at startup by `BigFatFile.Migrator`, under a PostgreSQL advisory lock so
          concurrent nodes don't race.
- Tenant isolation:
    - application-level scoping;
    - PostgreSQL row-level security (`Architecture.Tenancy.md`, section 2).
- Jobs and queues:
    - Hangfire with PostgreSQL storage is the single scheduling mechanism;
    - recurring jobs: send expiry, garbage collection, retention;
    - one-off work: malware scan after upload, thumbnails, notification emails, webhooks;
    - jobs receive identifiers, not objects;
    - jobs are retried with backoff;
    - dashboard under the admin area;
    - in cluster mode Hangfire coordinates across nodes through its PostgreSQL storage.
- Storage:
    - content-defined chunks written into warehouses (`Architecture.FileStorage.md`).
- Web UI (served from .NET):
    - Svelte 5 SPA built with Vite;
    - Tailwind CSS, shadcn-svelte, Lucide icons;
    - Paraglide (i18n);
    - TanStack Query (server state), TanStack Virtual, TanStack HotKeys.
- Authentication:
    - the Origin is the identity provider for the whole system (`Architecture.Tenancy.md`, section 4).

#### **BigFatFile.Replica** (.NET), aka the Replica:

- Server app acting as the full mirror and the on-prem backup of its tenant;
- Runs in an office on the local network (NAS, mini PC, Windows service);
- Local state:
    - SQLite (not PostgreSQL);
    - chunks in a `local` warehouse on the host's disks.
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
    - capacity planning: the tenant's deduplicated size plus retained versions;
    - applies the whole change feed without filtering; fetches missing chunks in the
      background, prioritising chunks LAN clients ask for;
    - a verification job compares every manifest against the chunks on disk, re-fetches
      anything missing, and reports status to the Origin ("Replica complete as of
      `<time>`" in the web UI).
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
    - writes are accepted at LAN speed, acknowledged, and queued in a durable journal
      (outbox).
- Offline:
    - reads and writes continue; LAN clients keep receiving change events;
    - all LAN writes pass through one Replica, so the LAN side has one linear history and
      conflicts between two LAN users are detected immediately.
- Reconnect:
    - pull the Origin's changes since the stored cursor and emit them as local events;
    - replay the journal in order, each operation carrying its base version;
    - on a rejected write, upload the LAN version as a conflict copy and keep the
      Origin's version as primary (`Architecture.ClientSynchronization.md`, section 3);
    - chunks are uploaded before the manifest that references them, so a half-uploaded
      file is never visible.
- Authentication on the LAN:
    - tokens are issued by the Origin; the Replica verifies them offline with the Origin's
      public signing key and enforces permissions from its local copy;
    - LAN tokens get a longer grace period so an outage does not sign everyone out.
- Admin page:
    - Svelte 5 app (shared UI stack);
    - served on the local network.

#### **BigFatFile.Desktop.(Linux, Windows, MacOS)** (.NET):

- A sync engine with OS integration;
    - shows the whole remote drive immediately, however large;
    - fetches file content on first open into a managed local cache (LRU);
- Uses materialisation model (avoid filesystem drivers where possible):
    - real folder entries that are hollow until opened ([Architecture.FileMaterialisation.md](Architecture.FileMaterialisation.md)).
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

#### **BigFatFile.Cli**:

- Client-side commands built over the client SDK.
- Scripted uploads and downloads, folder sync, admin tasks, CI usage;

### Libraries

- **BigFatFile.Contracts**:
    - everything the Origin, the Replica and every client must agree on exactly:
        - request and response types, error codes, real-time message types;
        - the chunker and its parameters, the hash function and the manifest format;
        - ID generation, name normalisation, path limits, the conflict-copy naming rule.
    - no dependencies beyond the base classes;
    - covered by test vectors (JSON fixtures) that the TypeScript implementation also runs.
- **BigFatFile.Abstractions**:
    - the seam between the open core and the licensed modules;
    - `IBffModule` (module registration, endpoints, embedded migrations), `IFeatureGate`;
    - interfaces the core consumes: `IChunkStore`, `IWarehouseRegistry`, `IChangeNotifier`,
      `IAuthProvider`, `IMalwareScanner`, `IPlanPolicy`, `IGroupDirectory`;
    - domain events modules subscribe to; access to the tenant context, audit writer,
      job registration and encrypted per-tenant settings;
    - changes rarely, because the private repository compiles against it.
- **BigFatFile.Client** (the SDK, published to NuGet):
    - typed API client over Contracts from the OpenAPI spec;
    - authentication flows (credentials, PKCE etc.) and a pluggable secure token store;
    - the transfer engine: chunk, dedup check, parallel upload with resume, verified
      download, presigned direct-to-storage when possible, bandwidth limits, retries;
    - real-time subscription to change notification events;
    - endpoint discovery and failover between Origin and Replica.
- **@bigfatfile/client** (npm):
    - `BigFatFile.Client` ported for the browser and Node;
    - the chunker and hasher in WebAssembly, in a web worker (a `worker_thread` on Node);
    - the transfer engine is separated (`@bigfatfile/client/transfer`),
      so Apps that only read folders can avoid bundling it;
    - resumes a partial upload after a page reload from the session ID and the file's
      fingerprint kept in `localStorage`.
- **@bigfatfile/ui** (npm):
    - Svelte 5 components wrapped as web components:
        - file browser, uploader, share dialog, file-request form;
    - React/Vue/etc. apps embed them with an endpoint and a token;
    - styled by the shared design system, themable by tokens.
- **BigFatFile.Testing** (NuGet):
    - a battery of integration tests for a BFF App;
    - a Testcontainers module that starts a real Origin with a seeded tenant.
- **BigFatFile.Sync**:
    - local state, journal, reconciliation, conflict policy, cache and prefetch policy,
      delta detection, the local API;
    - platform-neutral; used by Desktop, Replica and Cli;
    - its SQLite schema is versioned as `sync_<number>_<name>.sql` files, run by
      `BigFatFile.Migrator`.
- **BigFatFile.Migrator**:
    - our own schema migration runner, on Dapper over `DbConnection`, so it runs on
      PostgreSQL (Origin) and SQLite (Replica, Desktop);
    - finds `<prefix>_<number>_<name>.sql` files embedded in the assemblies it is given;
      each prefix has its own number sequence (`core_`, `sync_`, `sso_`);
    - records what ran in a `schema_migrations` table with a checksum per file, and
      refuses to start if an applied file has changed since;
    - applies pending files in order, each in its own transaction, under a lock:
      `pg_advisory_lock` on PostgreSQL, `BEGIN IMMEDIATE` on SQLite;
    - a file starting with `-- no-transaction` runs outside a transaction (for
      `CREATE INDEX CONCURRENTLY` and the like);
    - knows nothing about BFF tables; the SQL files live with the project that owns the
      tables.
- **Licensed modules** (private repository):
    - the modules listed in section 7, one project each;
    - compile against `Abstractions`;
    - loaded at startup;
    - enabled by a licence key.

### Dependency graph

```
BigFatFile.Contracts
        ^      ^
        |      +-- BigFatFile.Abstractions
        |              ^     ^     ^
        |              |     |     +-- licensed modules (private repository)
        |              |     +-- open-core modules (modules/)
        |              +-- BigFatFile.Server (the Origin) --> api/openapi.json
        |
BigFatFile.Client <-- BigFatFile.Testing
        ^
        |
BigFatFile.Sync
        ^
        +-- BigFatFile.Desktop
        +-- BigFatFile.Replica
        +-- BigFatFile.Cli

BigFatFile.Migrator
        ^
        +-- BigFatFile.Server   (core_ files, PostgreSQL advisory lock)
        +-- BigFatFile.Sync     (sync_ files, SQLite)

api/openapi.json --> @bigfatfile/client --> @bigfatfile/ui --> apps/web,
                                                              desktop settings UI,
                                                              Replica admin UI
```

- An arrow points from the dependency to what depends on it; `api/openapi.json` is
  generated from the Origin and is the source of the TypeScript types.
- `BigFatFile.Migrator` is used by Server (PostgreSQL) and Sync (SQLite); modules only
  embed their SQL files and do not reference it.
- Domain rules (permissions, quotas, plans), database tables and anything that
  references ASP.NET or the database stay in Server and are never shared with clients.

## 3. Apps

- An App is a first-class object in the Origin:
    - has its own tables and admin page;
    - name, owner, OAuth client (scopes, redirect URLs), webhook endpoint, metadata namespace;
    - an App acts as a signed-in user (authorization code with PKCE) or as itself on
      behalf of a tenant (service account, client credentials);
    - every action an App takes is audited under the App's identity and the acting
      user, if any.
- Installing:
    - the tenant administrator installs and manages Apps;
    - the admin sees the scopes requested by an App, and can revoke them at any time.
- Developer role per tenant:
    - creates Apps, rotates keys, sets webhook endpoints;
    - sees webhook delivery logs and API usage for its Apps.
- Scopes are coarse and readable:
    - `files.read`;
    - `files.write`;
    - `shares.manage`;
    - `requests.manage`;
    - `members.read`;
    - `members.manage`;
    - `metadata.write`;
    - `events.read`;
    - `tenant.mirror` (for tenant-wide mirroring or backup Apps, like Replica).

## 4. Primitives

Designed for Apps rather than discovered by them. Our own clients use these.

- Files and folders:
    - the node API, with cursor-paged listings, moves, versions, trash.
- Transfers:
    - `bff.files.upload(folder, input)` and `bff.files.download(id)` in the SDKs;
    - the SDK picks the file-level or the chunk-level endpoints; an App never sees
      chunks (`Architecture.API.md`).
- Change feed:
    - `GET /changes?since=<seq>`, the same cursor semantics the desktop client
      uses;
    - an App may follow one tenant or one folder subtree.
- Webhooks:
    - subscriptions per App to named events (file created in folder X, file request
      completed, member added, share link accessed);
    - signed payloads (HMAC with a per-App secret);
    - retries with backoff through Hangfire,
    - a delivery log;
    - support replay from the developer page.
- File requests:
    - a link that lets someone upload into a chosen folder without an account;
    - optional password and expiry;
    - optional required fields (period, document type) stored as metadata on the uploaded files;
    - example: the accounting portal's "upload portal" for a firm's customers.
- Custom metadata:
    - a key-value set per node under the App's namespace (`anaf.period = 2026-09`);
    - indexed and searchable; the Origin stores it and never interprets it.
- App-managed folders:
    - an App may own a subtree;
    - the policy says users may read and only the App writes;
    - the App's own data model (e.g. invoices, deadlines) are kept in the App's database,
      keyed by node IDs;
    - BFF stores files, the App stores meaning (semantics).
- Guest memberships and share links, sends, groups and permissions:
    - provided via the same API, with the same scopes.

## 5. Public API

- The one public API of the Origin, `/api/v1`: conventions, authentication, scopes and
  the endpoint list; the chunk-level endpoints are part of it under an "advanced" tag.
- [Architecture.API.md](Architecture.API.md)

## 6. SDKs and UI packages

- Python SDK follows when an App needs it:
    - the transfer engine is the part to port, the rest is generated.
- Versioning:
    - SDKs follow semantic versioning;
    - a compatibility matrix states which SDK versions work with which Origin versions;
    - the Origin reports its API version in a header and the SDK checks it on first call.

## 7. Licensed modules

### Core vs. platform modules

- Open core (AGPL):
    - all projects in [section 2](#_2-projects) except the licensed modules;
    - it is a complete product for a team, including:
        - cluster mode;
        - the PostgreSQL notifier;
        - local and S3 warehouses;
        - ClamAV scanning;
        - the desktop client;
        - the Replica;
        - the CLI.
- Licensed modules (aka "enterprise modules"):
    - advanced enterprise features (for the org's IT, compliance or finance depts.);
    - also features needed to host BFF as a service for others.
- Self-hosting:
    - hosting BFF for yourself and your own organisations is free.
    - hosting it as a service for others needs the enterprise modules.

### How a module plugs in

- A module is one assembly that compiles against `Abstractions`;
    - implements `IBffModule`:
        - `Name`, `RequiredCoreVersion`;
        - `ConfigureServices`: registers its services and the core interfaces it implements;
        - `MapEndpoints`: its endpoints under `/api/<module-prefix>/`;
    - migrations:
        - SQL files embedded in the assembly;
        - versioned under the module's own prefix and number sequence (for example
          `sso_0001_tenant_providers.sql`);
        - run by `BigFatFile.Migrator` at startup, after the core's own files;
        - may reference core tables, never another module's tables.
- Modules own their DB tables and API endpoints;
- Modules reach core data only through the core service (not directly);
- Loading:
    - at startup the Origin scans `/app/modules/` (mountable path);
    - loads each assembly in its own `AssemblyLoadContext` with `Abstractions` shared;
    - checks `RequiredCoreVersion`;
    - calls `ConfigureServices`;
    - runs the module's DB migrations;
    - maps the module's API endpoints;
    - the public image has the loader and no modules.
- Licence key:
    - a signed document (Ed25519; the public key is embedded in the Origin): instance or
      tenant ID, enabled feature keys, seat limit, expiry, grace period;
    - verified offline, so a dedicated instance without internet still starts;
    - A single way to check the key (`IFeatureGate.IsEnabled("<key>")`):
        - used by the core (show or hide UI, refuse endpoints);
        - used by modules (refuse to activate without their key).
- UI:
    - `/api/ui/manifest` lists enabled features, menu entries and settings pages;
    - the licensed web build includes the modules' UI packages from the private part of
      the workspace; pages appear only when the manifest enables them.
- Versioning:
    - `Abstractions` uses semantic versioning;
    - a module declares the core major version it targets;
    - the Origin refuses to load the module if versions don't match.

### Modules

Identity and access:

| Module                  | Key                 | Adds                                                                                                                          | Open core keeps                                           |
| ----------------------- | ------------------- | ----------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------- |
| SSO management          | `sso.tenant-config` | per-tenant identity provider settings with an admin UI; domain enforcement (everyone at a domain must use SSO, passwords off) | one server-wide OIDC provider, configured by the operator |
| SAML 2.0                | `sso.saml`          | SAML sign-in                                                                                                                  | OIDC                                                      |
| SCIM provisioning       | `sso.scim`          | the directory pushes user creates, updates and deactivations                                                                  | invitations, just-in-time provisioning                    |
| Directory group sync    | `sso.groups`        | directory groups become BFF groups, kept in sync                                                                              | manually managed groups                                   |
| LDAP / Active Directory | `sso.ldap`          | bind against on-premises AD, including from a dedicated instance inside the client's network                                  | none                                                      |
| Access policies         | `access.policies`   | mandatory 2FA per tenant, IP allowlists, session limits, client version requirements                                          | per-user 2FA and passkeys                                 |

Compliance and governance:

| Module                        | Key                    | Adds                                                                                                    | Open core keeps                                       |
| ----------------------------- | ---------------------- | ------------------------------------------------------------------------------------------------------- | ----------------------------------------------------- |
| Audit retention and reporting | `audit.extended`       | retention beyond the default; search, filter, export; scheduled reports; SIEM export                    | the audit log with default retention and a basic view |
| Retention and legal hold      | `governance.retention` | auto-delete rules, version retention per folder, legal hold, immutable folders with time-limited unlock | trash and the plan's version history                  |
| Data loss prevention          | `governance.dlp`       | content rules (CNP, IBAN, card numbers), block or flag sharing, classification labels                   | none                                                  |
| Admin search and discovery    | `governance.discovery` | search across the whole tenant for investigations and exports                                           | user-scoped search                                    |

Security:

| Module                     | Key                   | Adds                                                                                                         | Open core keeps                                     |
| -------------------------- | --------------------- | ------------------------------------------------------------------------------------------------------------ | --------------------------------------------------- |
| Advanced threat protection | `security.atp`        | commercial scanning engines, shared hash blocklist, quarantine workflow, scan policies                       | ClamAV scanning on upload                           |
| Ransomware protection      | `security.ransomware` | mass-change detection, honeypot files, automatic sync pause, one-click restore of a drive to a point in time | version history, manual restore of a file or folder |
| Customer-managed keys      | `security.byok`       | at-rest encryption with keys in the client's key management service                                          | storage-provider encryption                         |
| Device management          | `ops.devices`         | enrolled desktop clients per tenant, remote cache wipe, pin policies per group, block unmanaged devices      | user-managed pins and devices                       |

Sharing and branding:

| Module                        | Key                 | Adds                                                                                                                      | Open core keeps              |
| ----------------------------- | ------------------- | ------------------------------------------------------------------------------------------------------------------------- | ---------------------------- |
| White label and custom domain | `brand.whitelabel`  | tenant logo and colours on send and share pages, custom domain, email templates, branded desktop client                   | BFF branding                 |
| Sharing policies              | `sharing.policies`  | tenant-wide rules: external sharing on or off, mandatory passwords and expiry, recipient domain allowlists, link approval | per-link password and expiry |
| Protected viewing             | `sharing.protected` | dynamic watermarks, view-only links without download                                                                      | normal links                 |

Storage and operations:

| Module                  | Key                         | Adds                                                                                            | Open core keeps                                   |
| ----------------------- | --------------------------- | ----------------------------------------------------------------------------------------------- | ------------------------------------------------- |
| Tenant-owned storage    | `storage.tenant-warehouses` | warehouses defined per tenant from the web UI (the tenant's own bucket)                         | operator-defined warehouses from configuration    |
| Insights and monitoring | `ops.insights`              | usage analytics, storage trends, per-user activity, Replica health across many Replicas, alerts | per-tenant usage numbers and one Replica's status |

Integrations:

| Module                      | Key                       | Adds                                                                               | Open core keeps      |
| --------------------------- | ------------------------- | ---------------------------------------------------------------------------------- | -------------------- |
| Online editing              | `integrations.office`     | co-editing through Collabora, OnlyOffice or Microsoft 365 (WOPI)                   | file preview         |
| External storage connectors | `integrations.external`   | SharePoint, Google Drive and foreign S3 buckets mounted as folders                 | none                 |
| Workflow automation         | `integrations.automation` | rules (file arrives in X, do Y), approval flows, connectors to automation services | webhooks and the API |

Platform (needed to host BFF as a service):

| Module               | Key                | Adds                                                                         | Open core keeps                                                          |
| -------------------- | ------------------ | ---------------------------------------------------------------------------- | ------------------------------------------------------------------------ |
| Billing              | `platform.billing` | plans, Stripe, invoices, quota enforcement by plan                           | one unlimited plan                                                       |
| Abuse moderation     | `platform.abuse`   | report queue, takedown workflow, hash blocklist, rate limits on public sends | the "report abuse" link, which emails the operator                       |
| Multi-tenant hosting | `platform.hosting` | tenant provisioning, plan assignment, usage metering, support impersonation  | multi-tenancy itself; a self-hoster may run several tenants of their own |

## 8. The developer experience

- `bff dev up` command:
    - few minutes from clone to first upload;
    - command starts (via a compose file):
        - an Origin with PostgreSQL;
        - a seeded demo tenant;
        - a ready-made App with keys;
        - the Aspire dashboard for traces and logs.
- Demo approach:
    - a public sandbox tenant on the cloud;
    - resets nightly, for trying the API without installing anything.
- HTTP endpoint `/api/docs` on every instance:
    - the reference docs, generated from the committed spec,
      with "try it" against that instance.
- `docs/developers/`:
    - quickstart: get a token, upload a file, create a file request, get the webhook;
    - one guide per primitive in section 4;
    - SDK guides;
    - an upgrade guide per API version;
    - the changelog.
- `examples/`:
    - small, complete samples: a CI artifact uploader, a backup script, a web page
      embedding the file browser, a file-request portal.
- For module authors (section 7):
    - `dotnet new bff-module`;
    - contract tests against `Abstractions`;
    - hot reload in the development loop.
