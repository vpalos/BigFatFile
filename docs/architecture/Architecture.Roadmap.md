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
- The Origin skeleton:
    - PostgreSQL;
    - Dapper;
    - schema migrations with Evolve, starting with the core's scripts;
    - Identity and OpenIddict;
    - Hangfire;
    - the module loader with no modules;
    - the feature gate with reserved keys;
    - OpenTelemetry instrumentation:
        - OTLP via environment variables;
        - Prometheus endpoint;
        - Aspire dashboard in the development.
- Commands:
    - `./dev up` (start locally)
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

- Status: steps 1, 2, 3 and 7 are done.
- Steps, in order:
    1. The API spec, with every endpoint of M1.1 as a stub.
    2. Tenant isolation:
        - the application connects as a role that does not own the tables;
        - row-level security on every tenant-owned table;
        - the tenant a session or a token is scoped to, chosen at sign-in;
        - the isolation test set.
    3. Telemetry, plans and audit:
        - the `BigFatFile` activity source and meter in `Abstractions`;
        - the one unlimited plan behind `IPlanPolicy`;
        - the audit log, partitioned, starting with sign-ins.
    4. Accounts:
        - sign-up with email and password, email verification and password reset,
          through MailKit;
        - Mailpit in the development compose file, to catch those emails;
        - `BFF_SIGN_UP`, `BFF_OPERATOR_EMAIL`, and `BFF_PASSWORD_SIGN_IN` to turn
          password sign-in off;
        - the server-rendered sign-in form removed.
    5. Members and invitations:
        - members, roles and invitations (`members.*` endpoints);
        - the invitation email.
    6. External sign-in:
        - Google and Microsoft;
        - the operator's one OpenID Connect provider.
    7. CSRF protection for write operations that use the session cookie.
    8. The SDK calls for the account, tenant and member endpoints, in
       `@bigfatfile/client` and `BigFatFile.Client`.
    9. The web app skeleton, on `@bigfatfile/client`:
        - the Svelte 5 stack;
        - the sign-up, sign-in and members pages.
    10. The reference deployment behind TLS:
        - the `proxy` compose profile (Caddy);
        - forwarded headers from the proxy;
        - connection pool sizes that fit PostgreSQL's `max_connections`.
- Done when:
    - an invited person signs up from the email and lands in the tenant;
    - every endpoint, called with another tenant's IDs, answers 404;
    - an Origin behind the `proxy` profile signs users in over HTTPS.

#### M1.2. The file tree

- Drives, each with its root and trash.
- Nodes:
    - create a folder, rename, move, trash and restore, with `If-Match`;
    - cursor-paged listings, the path and lookup by path;
    - client-generated node IDs; `Idempotency-Key` on every other request that creates
      something.
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
- The content tables (`Architecture.DataModel.md`, section 3):
    - `node_versions`, `manifests`, `manifest_chunks`, `chunks` and `objects`.
- Whole objects and `none` manifests:
    - the switch that turns off chunking on the Origin, per tenant and per upload;
    - the tenant's default chunking, which may be `none`;
    - where the API and the data model keep those two, decided in this milestone.
- File-level upload with inline chunking and offset resume.
- Downloads with `Range`, for previews and playback.
- Versions: list and restore.
- The first CLI commands: sign in (PKCE, device code), `ls`, upload and download.
- Done when:
    - a file that is uploaded from the web app and a file that is uploaded from the CLI
      both download identically, byte for byte;
    - an interrupted upload resumes from its offset;
    - an earlier version is restored.

#### M1.4. Chunk-level transfer

- Chunked uploads keyed by the node ID:
    - the missing chunks;
    - a hash check on every chunk;
    - presigned `PUT` URLs on S3, in batches, from `POST /nodes/{id}/upload/grants`;
    - completion with `If-Match`.
- Manifest downloads:
    - chunks from the Origin, through `GET /nodes/{id}/chunks/{hash}`;
    - presigned `GET` URLs on S3, in batches, from `POST /nodes/{id}/chunks/grants`.
- The chunker and hasher in WebAssembly, built from the reference crates, passing the
  shared test vectors.
- The transfer engines in both SDKs: parallel chunks over several connections, the
  adaptive in-flight count, per-chunk retry and resume, delta against the last version.
- The SDKs choose between file-level and chunk-level transfer.
- Garbage collection: mark and sweep per tenant, upload pins, the 24-hour rule.
- Done when:
    - uploading a file that the tenant already has sends no chunks, from the web app
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
    - a change that is made in one browser appears in another within a second, on one
      node and across two;
    - a client whose cursor is older than the retention period resyncs;
    - the feed is the one the desktop client (M2) reads.

#### M1.6. Sharing

- Permission rules on drives and folders (viewer, editor, owner) and the effective
  permission.
- Groups, behind `IGroupDirectory`.
- Guest memberships.
- Share links: password, expiry, the public endpoints, multi-file links zipped in the
  browser.
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
- The client-credentials flow, for an App that acts as itself:
    - the token gets its tenant from the App's installation in that tenant;
    - the flow stays off until then, because a token without a tenant reaches nothing.
- Webhooks, custom metadata, App-managed folders.
- `@bigfatfile/ui` as Svelte and web components; `BigFatFile.Testing`.
- `docs/developers/` with the quickstart and one guide per primitive; `examples/`.
- Done when: a sample App outside the repository is built with the SDK alone, installs
  into a tenant and receives webhooks.

### M4. Replica

- The Replica adapter (full mirror, no eviction), pairing, status reporting, the
  `tenant.mirror` scope, bootstrap from the snapshot, token exchange for deferred
  replay; about a week of work once the engine exists.
- The chunk endpoints by hash, for `tenant.mirror`: `GET /nodes/chunks/{hash}` and
  `POST /nodes/chunks/exists`.
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
- The public pages (Privacy, FAQ, Features, Plans, About) rewritten:
    - file content passes through the Origin, which v1's pages said it did not;
    - the Privacy page says that the audit log stores the IP address and the user agent of
      each sign-in, and for how long it keeps them.
- Done when: v1 is switched off.

## 3. After v2.0

- macOS desktop (File Provider, Swift adapter), when there is demand or a Swift developer.
- The public sandbox tenant.
- The Python SDK, if an App needs it.
- Content-defined chunking parameters tuned per file type from production data.
- The remaining licensed modules, in the order customers ask for them.
