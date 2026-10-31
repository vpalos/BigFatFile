# BigFatFile

File storage and sync for teams: an Origin server, an optional Replica in the office, and
Apps (web, desktop, CLI and third-party) built on one public API. The design is in
[docs/architecture](docs/architecture/Architecture.md).

## 1. Requirements

- Docker with the Compose plugin, to run the Origin.
- To work on the code:
    - the .NET 10 SDK;
    - Node 24, with `corepack enable` run once so that `pnpm` is on the path.

## 2. Running locally

- `./bff dev up` builds the Origin from this checkout and starts it with PostgreSQL and
  the Aspire dashboard:
    - sign-in page: http://localhost:8080/account/sign-in
    - API reference: http://localhost:8080/api/docs
    - job dashboard: http://localhost:8080/admin/jobs
    - traces, metrics and logs: http://localhost:18888
- The development seed creates:
    - the user `demo@bigfatfile.local` with the password `bigfatfile-demo`, admin of the
      tenant "Demo";
    - the client `bff-dev-cli`: public, authorization code with PKCE, redirect
      `http://127.0.0.1:53682/callback`;
    - the client `bff-dev-app`: confidential, secret `bff-dev-app-secret`, client
      credentials.
- `./bff dev down` stops everything and keeps the data; `./bff dev reset` also deletes
  the data.
- Sign in from a script and call the API through the generated TypeScript types:

```sh
pnpm install && pnpm build
pnpm --filter @bigfatfile/example-sign-in start
```

- `dotnet run --project server/BigFatFile.Server` runs a second Origin on
  http://localhost:5080 against the same development database, for debugging.

## 3. Building and testing

- .NET:
    - `dotnet build BigFatFile.slnx` and `dotnet test --solution BigFatFile.slnx`;
    - the tests start PostgreSQL in Docker through Testcontainers;
    - every build of the Server rewrites `api/openapi.json`; commit it with the change
      that caused it.
- TypeScript:
    - `pnpm install`, then `pnpm generate` (types from `api/openapi.json`), `pnpm build`,
      `pnpm typecheck` and `pnpm test`.
- CI runs all of the above and fails when `api/openapi.json` or the generated types
  differ from what the build produces.

## 4. Releases

- A tag `vX.Y.Z` builds every artifact with that version:
    - the Origin image on GHCR, for linux/amd64 and linux/arm64, signed with cosign;
    - the NuGet packages `BigFatFile.Contracts`, `BigFatFile.Migrator` and
      `BigFatFile.Abstractions`;
    - the npm package `@bigfatfile/client`;
    - `api/openapi.json`, attached to the GitHub release.
- Publishing to NuGet and npm needs the repository secrets `NUGET_API_KEY` and
  `NPM_TOKEN`; without them the packages are only attached to the release.
- Every push to `main` publishes the image with the tag `edge`.
