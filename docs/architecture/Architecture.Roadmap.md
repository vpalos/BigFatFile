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
    - Badgie migrations;
    - Identity and OpenIddict;
    - Hangfire;
    - the module loader with no modules;
    - the feature gate with reserved keys;
    - built-in OpenTelemetry instrumentation, OTLP through environment variables, the
      Prometheus endpoint and the Aspire dashboard in the development compose.
- Commands:
    - `bff dev up` (start locally)
- Endpoints:
    - `/api/docs`
- Done when:
    - clone the repo;
    - run command to start locally;
    - sign in and call an API endpoint using the generated TS types.

### M1. Origin core

- Data model (tree, versions, change log, audit), warehouses (`local`, `s3`), chunk
  store and garbage collection.
- File-level upload with inline chunking and offset resume; chunk-level sessions and
  manifest downloads; sends as whole objects; file requests.
- Change feed and the real-time hub; permissions, groups, shares.
- The web app on `@bigfatfile/client`; the CLI on `BigFatFile.Client`.
- ClamAV scanning built as an `IBffModule` compiled into the core, to prove the module
  seam before a licensed module depends on it.
- The `BigFatFile` activity source and meter in `Abstractions`; spans and metrics added as
  each feature is built (upload sessions, downloads, garbage collection), not afterwards.
- Done when: the web app and the CLI reach v1 feature parity using only the published
  API and SDKs.

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
