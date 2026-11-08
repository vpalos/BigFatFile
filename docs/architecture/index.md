# BigFatFile Architecture (v2)

## 1. Goals

- A platform:
    - BFF supports the development of an ecosystem of apps (or even marketplace);
    - applications (aka BFF Apps) are built on the published API and SDKs;
    - BFF Apps are installed by tenant admins;
    - provided BFF clients are also BFF Apps (more in [Platform topology](./platform/topology.md));
- One codebase, three deployments:
    - the public cloud at bigfatfile.com;
    - dedicated single-tenant instances for specific clients;
    - self-hosted installs by anyone (running on Docker Compose).
- Local disk experience:
    - files open at the speed of a local disk;
    - an office keeps working undisturbed even if the internet connection is down.
- Security and protection:
    - data is protected against ransomware;
    - files are versioned, protecting against mistakes and losses;
    - data is encrypted in transit and at rest.
- Limits never slow real users:
    - transfer endpoints are never rate-limited;
    - abuse controls count and open a ticket before anything blocks
      ([File transfer](./files/transfer.md), section 6).
- Open-source core, plus licensed modules:
    - the core is AGPL and is a complete product for a team;
    - licensed modules that plug into the core ([Platform topology](./platform/topology.md), section 7).
- One UI stack:
    - all UIs are Svelte 5 apps that share design, components and translations.

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

docs/       architecture documents, developer docs
```

**Note on licensed modules:**

- These live in a private repository, separately;
- They compile against the `Abstractions` project from NuGet.

## 3. Chapters

- [Platform topology](./platform/topology.md):
    - the platform topology (Origin, Replica, Apps), the projects and their dependencies,
      jobs, SDKs, licensed modules and the developer experience.
- [API](./platform/api.md):
    - the one public API, `/api/v1`: conventions, authentication, scopes and the
      endpoint list; the chunk-level endpoints are part of it, with an "Advanced" badge.
- [Authentication](./security/authentication.md):
    - users, sessions, tokens and scopes, and how clients sign in;
    - tenants, tenant isolation (scoping and row-level security), groups and permissions.
- [Licensing](./platform/licensing.md):
    - the licence, the feature keys that turn licensed modules on, and the plan of each
      tenant.
- [Data Model](./files/data-model.md):
    - the file tree, uploads in progress, content, permissions, file versioning, the change
      log and the audit log.
- [File storage](./files/storage.md):
    - how drive files are stored using CDC, in warehouses.
- [File transfer](./files/transfer.md):
    - file transfer through the API, the API scopes and throughput optimization.
- [File materialisation](./files/materialisation.md):
    - how a desktop client shows the whole remote drive as a local folder with
      explorer (OS) integration, and how file changes are detected on each platform.
- [Client synchronization](./files/synchronization.md):
    - how clients learn about changes, the client sync loop and conflict handling.
- [Deployment](./operations/deployment.md):
    - how the Origin, the Replica, the desktop client and the CLI are packaged, installed
      and updated on each platform, and how images are signed.
- [Observability](./operations/observability.md):
    - traces, metrics and logs with OpenTelemetry; what BFF measures and where the data goes.
- [Roadmap](./roadmap.md):
    - milestones for v2 in order, with contents and definition of done.
