# BigFatFile Architecture (v2)

## 1. Goals

1. **The platform**:
    - supports development of apps via the published API and SDKs;
    - apps are installed by tenant admins;
    - BFF clients are also BFF Apps (more in [Topology](./platform/topology.md));
2. **Deployed from monorepo**:
    - the public cloud at bigfatfile.com;
    - self-hosted installs (running on Docker Compose).
3. **One UI stack**:
    - core BFF UIs share design, components and translations.
4. **Local disk UX**:
    - user files open at the speed of a local disk;
    - an office keeps working normally even if the internet connection is down.
5. **Security and protection**:
    - data is protected against ransomware;
    - files are versioned, protecting against mistakes and losses;
    - data is encrypted in transit and at rest.
6. **Open-source core**:
    - the core is AGPL and is a complete product for a team;
    - licensed modules that plug into the core ([Topology](./platform/topology.md), section 7).

## 2. Repository structure

```
sdk/        components used to build clients & Apps:
            BigFatFile.Contracts
            BigFatFile.Client
            BigFatFile.Sync
            BigFatFile.Testing
            @bigfatfile/client
            @bigfatfile/ui
            the shared design system
            test-vectors (the JSON fixtures every implementation runs)

api/        public API specification:
            openapi.json
            versioning policy
            change log

apps/       BFF Apps provided alongside the core:
            BigFatFile.Web
            BigFatFile.Desktop.(Linux, Windows, MacOS etc.)
            BigFatFile.Cli
            BigFatFile.Replica

server/     the Origin server (mostly the "core"):
            BigFatFile.Origin
            BigFatFile.Abstractions

modules/    the module template, open-core modules

examples/   small, complete samples

artwork/    draft branding, logos, raw cuts

bench/      benchmarks, prototypes

docs/       architecture documents, feature pages, developer docs
```

**Additional licensed modules:**

- The `Abstractions` sub-project (published on NuGet) is the interface between the
  open-source core and private modules, which are proprietary code (e.g. billing).

## 3. Chapters

- [Topology](./platform/topology.md):
    - the platform architecture in more detail, with components and deps.
- [API](./platform/api.md):
    - the public API; endpoints and conventions.
- [Tenancy](./security/tenancy.md):
    - users, accounts, tenants, roles, groups and permissions.
- [Authentication](./security/authentication.md):
    - how authentication and authorization are implemented.
- [Licensing](./platform/licensing.md):
    - details about plans, features and the split between open and licensed code.
- [Data Model](./files/data-model.md):
    - how the metadata is stored, audited and managed.
- [File storage](./files/storage.md):
    - how file data is stored using CDC, in warehouses.
- [File transfer](./files/transfer.md):
    - how file transfers work through the API and throughput optimization.
- [File materialisation](./files/materialisation.md):
    - how a client shows remote drives as local folders and OS integration.
- [Client synchronization](./files/synchronization.md):
    - how clients learn about changes, the sync loop and conflict handling.
- [Deployment](./operations/deployment.md):
    - how the binaries are packaged, signed, installed and updated on each platform.
- [Observability](./operations/observability.md):
    - OpenTelemetry integration; what BFF measures and where the data goes.
- [Roadmap](./roadmap.md):
    - milestones for v2 in order, with contents and definition of done.
