# BigFatFile

A platform that runs file storage and sync apps for teams.
The design is explained in detail in `docs/architecture`.

## 1. Requirements

- Docker with Compose plugin
- .NET 10 SDK
- Node 24 then run `corepack enable` once to enable `pnpm`.

## 2. Running locally

- `./bff dev up` starts the Origin server (hot-reload mode);
- `./bff dev logs` follows logs;
- `./bff dev down` stops everything and keeps the data;
- `./bff dev reset` stops everything and also deletes the data and the build cache;
- `./bff docs architecture` serves the architecture docs at http://localhost:3000.

**Notes:**

- Runs using `dotnet watch` and hot-reload;
    - the first start takes longer (restores packages, builds artifacts).

- Published URLs:
    - Sign-in page:
      http://localhost:8080/account/sign-in
    - API reference:
      http://localhost:8080/api/docs
    - Hangfire dashboard:
      http://localhost:8080/admin/jobs
    - Aspire dashboard:
      http://localhost:18888

- Seeded data:
    - tenant "Demo";
    - user `demo@bigfatfile.local` (password `bigfatfile-demo`, admin of tenant "Demo");
    - two clients:
        - `bff-dev-cli`:
            - public client;
            - authorization code is obtained via PKCE;
            - redirected to `http://127.0.0.1:53682/callback`;
        - `bff-dev-app`:
            - confidential client;
            - secret is `bff-dev-app-secret`.

## 3. Debugging

Run a separate Origin server at the command line, for active debugging.
Requires the dev Docker Compose infrastructure to be up.

```sh
dotnet run --project server/BigFatFile.Server
```

- published at: http://localhost:5080

## 4. Running examples

The sign-in example signs in from a script and calls the API through the generated TypeScript types:

```sh
pnpm install
pnpm build
pnpm --filter @bigfatfile/example-sign-in start
```

## 5. Building and testing

- .NET:
    - `dotnet build BigFatFile.slnx`, then...
    - `dotnet test --solution BigFatFile.slnx`;
    - the tests start PostgreSQL in Docker through Testcontainers;
    - every build of the Server rewrites `api/openapi.json`.
- TypeScript:
    - `pnpm install`
    - `pnpm generate` (generates types from `api/openapi.json`)
    - `pnpm build`
    - `pnpm typecheck`
    - `pnpm test`
- CI runs all of the above and fails when `api/openapi.json` or the generated types
  differ from what the build produces.

## 6. Releases

- A tag `vX.Y.Z` builds every artifact with that version:
    - the Origin image on GHCR, for linux/amd64 and linux/arm64, signed with cosign;
    - the NuGet packages:
        - `BigFatFile.Contracts`
        - `BigFatFile.Migrator`
        - `BigFatFile.Abstractions`
    - the npm package:
        - `@bigfatfile/client`
    - `api/openapi.json`, attached to the GitHub release.
- Publishing to NuGet and npm needs the repository secrets `NUGET_API_KEY` and
  `NPM_TOKEN`; without them the packages are only attached to the release.
- Every push to `main` publishes the image with the tag `edge`.
