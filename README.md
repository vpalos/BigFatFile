# BigFatFile

A platform that runs file storage and sync apps for teams.
The design is explained in detail in `docs/architecture`, and the reasons for it in `docs/decisions`.

## 1. Requirements

- Docker with Compose plugin
- .NET 10 SDK
- Node 24 then run `corepack enable` once to enable `pnpm`.

## 2. Running locally

- `./dev` with no command lists every command;
- `./dev up` starts the Origin server (hot-reload mode);
- `./dev logs` follows the Origin's logs:
    - `./dev logs <service>` follows the logs of another service, such as `db` or `aspire`;
- `./dev down` stops everything and keeps the data;
- `./dev restart` stops everything and starts it again, and keeps the data;
- `./dev purge` stops everything and also deletes the data and the build cache;
- `./dev psql` opens psql on the database, as the owner of the tables:
    - `./dev psql --app` signs in as `bigfatfile_app`, the role that row-level security
      applies to;
- `./dev docs` serves the docs site at http://localhost:3000, with live reload.

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
    - client `bff-dev-cli`:
        - public client;
        - authorization code is obtained via PKCE;
        - redirected to `http://127.0.0.1:53682/callback`.

## 3. Debugging

Run a separate Origin server at the command line, for active debugging.
Requires the dev Docker Compose infrastructure to be up.

```sh
dotnet run --project server/BigFatFile.Origin
```

- published at: http://localhost:5080

## 4. Running examples

The sign-in example signs in from a script and calls the API through the generated TypeScript types.
With `./dev up` running:

```sh
./dev example sign-in
```

- the command builds the packages that the example uses, then starts the example;
- `./dev example` lists the examples.

## 5. Building and testing

- `./dev build` builds everything, in this order:
    - `dotnet build BigFatFile.slnx`, which also rewrites `api/openapi.json`;
    - `pnpm install`;
    - `pnpm generate`, which generates the TypeScript types from `api/openapi.json`;
    - `pnpm build`;
    - `pnpm docs:build`, which also fails on a link to a page that does not exist.
- `./dev test` runs the tests, in this order:
    - `dotnet test --solution BigFatFile.slnx`, which starts PostgreSQL in Docker through
      Testcontainers;
    - `pnpm typecheck`;
    - `pnpm test`.
- `./dev check` runs what CI runs, before a push:
    - `./dev build`;
    - a stop when the build changed `api/openapi.json`, the generated types or
      `pnpm-lock.yaml`, compared with what is staged;
    - `./dev test`.
- CI runs all of the above and fails when `api/openapi.json` or the generated types
  differ from what the build produces.

## 6. Releases

- A tag `vX.Y.Z` builds every artifact with that version:
    - the Origin image on GHCR, for linux/amd64 and linux/arm64, signed with cosign;
    - the NuGet packages:
        - `BigFatFile.Contracts`
        - `BigFatFile.Abstractions`
    - the npm package:
        - `@bigfatfile/client`
    - `api/openapi.json`, attached to the GitHub release.
- Publishing to NuGet and npm needs the repository secrets `NUGET_API_KEY` and
  `NPM_TOKEN`; without them the packages are only attached to the release.
- Every push to `main` publishes the image with the tag `edge`.
