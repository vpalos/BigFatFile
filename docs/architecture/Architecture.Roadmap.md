# BigFatFile Architecture: Roadmap

Milestones for v2, in order, each with what it contains and when it counts as done.

## 1. Principles

- The platform is built in parallel with the core, not after it:
    - each feature's API is designed in the spec first and reviewed as a diff;
    - the SDK grows with it;
    - the web app is built on the SDK from the first screen.
- Migration (v1->v2);
    - v1 stays in production until v2 reaches parity;
    - the two run side by side during the migration.
- Every milestone ends with a check:
    - the thing it delivers is used by the next milestone, not only demonstrated.

## 2. Milestones

### M0. Bootstrapping

- Status: done.
- The repository as in `Architecture.md` section 2:
    - CI building images;
    - packages;
    - the OpenAPI specification from one tag;
    - TS types generated from the OpenAPI spec.
- `BigFatFile.Contracts`:
    - chunker;
    - hash;
    - manifest format;
    - name normalisation;
    - the shared test vectors (started).
- `BigFatFile.Migrator`:
    - the `core_` migration files;
- The Origin skeleton:
    - PostgreSQL;
    - Dapper;
    - Identity and OpenIddict;
    - Hangfire;
    - the module loader with no modules;
    - the feature gate with reserved keys;
    - OpenTelemetry instrumentation:
        - OTLP via environment variables;
        - Prometheus endpoint;
        - Aspire dashboard in the development.
- Commands:
    - `bff dev up` (start locally)
- Endpoints:
    - `/api/docs`
- Done when:
    - clone the repo;
    - run command to start locally;
    - sign in and call an API endpoint using the generated TS types.

### M1. Origin core

- Seven parts, in order, each ending with its own check.
- Every part brings, for what it builds:
    - the endpoints, in the spec first;
    - the SDK calls, in `@bigfatfile/client` and `BigFatFile.Client`;
    - the web app screens, on `@bigfatfile/client`;
    - the CLI commands, on `BigFatFile.Client`;
    - the spans and metrics (`Architecture.Observability.md`, section 2), not
      afterwards.
- M1 is done when M1.7 is.

#### M1.1. Accounts and tenants

- Accounts:
    - sign-up with email and password, email verification and password reset, through
      MailKit;
    - Google and Microsoft sign-in;
    - the operator's one OpenID Connect provider.
- Tenants:
    - members, roles and invitations (`members.*` endpoints);
    - the tenant a token is scoped to, chosen at sign-in;
    - the one unlimited plan behind `IPlanPolicy`.
- Tenant isolation:
    - the application connects as a role that does not own the tables;
    - row-level security on every tenant-owned table;
    - the isolation test set.
- The audit log, starting with sign-ins.
- The web app skeleton: the Svelte 5 stack, sign-up, sign-in and the members page.
- CSRF protection for writes made with the session cookie.
- The reference deployment behind TLS:
    - the `proxy` compose profile (Caddy);
    - forwarded headers from the proxy;
    - connection pool sizes that fit PostgreSQL's `max_connections`.
- The `BigFatFile` activity source and meter in `Abstractions`.
- Done when:
    - an invited person signs up from the email and lands in the tenant;
    - every endpoint, called with another tenant's IDs, answers 404;
    - an Origin behind the `proxy` profile signs users in over HTTPS.

#### M1.2. The file tree

- Drives, each with its root and trash.
- Nodes:
    - create a folder, rename, move, trash and restore, with `If-Match`;
    - cursor-paged listings, the path and lookup by path;
    - `Idempotency-Key` on every create.
- The change log, one row per tree change in the same transaction, and the tenant's
  sequence counter.
- The web app's folder browser.
- Done when:
    - a user builds and rearranges a folder tree in the web app;
    - every change has its change-log row and its audit entry;
    - a stale `If-Match` gets 412.

#### M1.3. Files

- Warehouses (`local`, `s3`) from the operator's file, behind `IWarehouseRegistry`, with
  the startup probes.
- The chunk store behind `IChunkStore`.
- Manifests in the database and in the warehouse, in the commit order of
  `Architecture.FileStorage.md`.
- File-level upload with inline chunking and offset resume.
- Downloads with `Range`, for previews and playback.
- Versions: list and restore.
- The first CLI commands: sign in (PKCE, device code), `ls`, upload and download.
- Done when:
    - a file uploaded from the web app and one from the CLI download byte for byte;
    - an interrupted upload resumes from its offset;
    - an earlier version is restored.

#### M1.4. Chunk-level transfer

- Upload sessions: the missing chunks, presigned URLs on S3, a hash check on every
  chunk, completion with `If-Match`.
- Manifest downloads with chunk grants.
- `POST /transfer/chunks/exists`.
- The chunker and hasher in WebAssembly, built from the reference crates, passing the
  shared test vectors.
- The transfer engines in both SDKs: parallel chunks over several connections, the
  adaptive in-flight count, per-chunk retry and resume, delta against the last version.
- The SDKs choose between file-level and chunk-level transfer.
- Garbage collection: mark and sweep per tenant, session pins, the 24-hour rule.
- Done when:
    - a file the tenant already has uploads without sending a chunk, from the web app
      and from the CLI;
    - an edit to a large file uploads only its changed chunks;
    - garbage collection deletes unreferenced chunks and keeps those of uploads in
      progress.

#### M1.5. Change feed

- `GET /changes` from a cursor, for a tenant or a subtree.
- Retention, and the full resync for a cursor older than it.
- The real-time hub with `ChangesAvailable`, behind `IChangeNotifier`:
    - the in-process bus on one node;
    - PostgreSQL `NOTIFY` in cluster mode, with the two-node compose file in the
      integration tests.
- The SDK subscription.
- Live updates in the web app.
- Done when:
    - a change made in one browser shows in another within a second, on one node and
      across two;
    - a client whose cursor is older than the retention period resyncs;
    - the feed is the one the desktop client (M2) reads.

#### M1.6. Sharing

- Permission rules on nodes (viewer, editor, owner) and the effective permission.
- Groups, behind `IGroupDirectory`.
- Guest memberships.
- Share links: password, expiry, the public endpoints, multi-file links as a streamed
  zip.
- Audit entries for permission changes, share-link visits and downloads.
- Done when:
    - a group with editor rights on a folder works in it, and nobody else sees it;
    - an outsider opens a password-protected share link, and the visit is in the audit
      log.

#### M1.7. Sends, file requests and scanning

- Sends as whole objects:
    - presigned multipart upload on S3, `PUT` on `local`;
    - passwords, expiry and the expiry job;
    - the public download pages and the "report abuse" link.
- File requests: password, expiry and required fields stored as metadata.
- Notification emails.
- ClamAV scanning built as an `IBffModule` compiled into the core, behind
  `IMalwareScanner`, to prove the module seam before a licensed module depends on it.
- Done when:
    - every upload path is scanned;
    - the web app and the CLI reach v1 feature parity using only the published API and
      SDKs, except billing and abuse moderation, which are licensed modules (M5).

### M2. Desktop

- `BigFatFile.Sync` with the in-memory adapter and the contract test suite.
- Linux adapter (FUSE with the write-back cache) first, to harden the engine on the
  development machine.
- Windows adapter (Cloud Files API), the Photino settings window, MSIX packaging.
- Local diagnostics in the settings window and `bff diag`; the telemetry opt-in switch.
- Estimates:

|         | Prototype                          | Customer-ready | Main risk                                           |
| ------- | ---------------------------------- | -------------- | --------------------------------------------------- |
| Linux   | 3 to 5 weeks                       | 6 to 8 weeks   | filesystem semantics and write-back correctness     |
| Windows | 2 to 4 weeks                       | 2 to 3 months  | thin documentation; behaviour discovered by testing |
| macOS   | 2 to 3 weeks with Swift experience | 1 to 2 months  | toolchain, signing, opaque system behaviour         |

- Done when: a 20 TB test drive is visible within seconds of sign-in, files open at
  local-disk speed after the first access, and an edit uploads only its changed chunks.

### M3. Platform

- Apps as first-class objects: registration, scopes, installation per tenant, the
  developer role and page.
- Webhooks, custom metadata, App-managed folders.
- `@bigfatfile/ui` as Svelte and web components; `BigFatFile.Testing`.
- `docs/developers/` with the quickstart and one guide per primitive; `examples/`.
- Done when: a sample App outside the repository is built with the SDK alone, installs
  into a tenant and receives webhooks.

### M4. Replica

- The Replica adapter (full mirror, no eviction), pairing, status reporting, the
  `tenant.mirror` scope, bootstrap from the snapshot, token exchange for deferred
  replay; about a week of work once the engine exists.
- Replica reconcile spans and metrics (lag, outbox depth, verification).
- Docker image for NAS hosts; the Windows service build.
- Done when: an office keeps reading and writing with the uplink cut, and the Origin is
  consistent after reconnect with conflicts surfaced as conflict copies.

### M5. Licensed modules, first release

- `platform.billing`, `platform.abuse`, `platform.hosting`, the `sso.*` group,
  `audit.extended`, `brand.whitelabel`.
- The licensed image built by the private pipeline; the licence key verified offline.
- Done when: the cloud runs the licensed image, a paying tenant exists, and a dedicated
  instance starts with a key and no internet.

### M6. Launch

- Observability wired end to end; the `observability` compose profile and the alert
  list.
- `bff backup` and `bff restore`; the v1 data migration run against a production copy,
  then for real.
- Deployment catalogs (Unraid, TrueNAS, CasaOS, Portainer, Coolify) and the package
  repositories.
- Done when: v1 is switched off.

## 3. After v2.0

- macOS desktop (File Provider, Swift adapter), when there is demand or a Swift developer.
- The public sandbox tenant.
- The Python SDK, if an App needs it.
- Content-defined chunking parameters tuned per file type from production data.
- The remaining licensed modules, in the order customers ask for them.
