# BigFatFile Architecture: Platform

How other applications are built on BigFatFile: Apps, the primitives they use, the API
conventions, the SDKs and UI packages, and the developer loop. Referenced from
`Architecture.md`. The in-process side (modules that extend the Origin itself) is in
`Architecture.Modules.md`.

## 1. The rule

- Our own Apps are third-party Apps:
    - the web app, the desktop client, the CLI and the Replica use only the published
      API and SDKs;
    - no private endpoints, no internal shortcuts; the platform is the Origin alone.
- Two axes describe any App:
    - level: file-level Apps use the `files` endpoints; chunk-level Apps use the
      `transfer` endpoints directly (the desktop client, the CLI, the Replica, a backup
      agent);
    - scope: node-scoped Apps act on what their user may see; tenant-scoped Apps hold
      `tenant.mirror` and see the whole tenant.
- The Replica is a first-party, chunk-level, tenant-scoped App that also serves the
  API downstream to the office (`Architecture.API.md` section 4, `Architecture.Transfer.md` section 4).
- Consequences:
    - anything our UI can do, an outside app can do;
    - the API is complete on day one, because it had to be;
    - the SDKs are exercised by our own apps before anyone else sees them.
- Acceptance test for v2:
    - our own Apps (the web app, the desktop client, the CLI and the Replica) are built
      with the published API and SDKs and nothing else, before the API is called done.
- A separate project, an accounting-firm portal for ANAF document exchange, is the kind
  of App this is for and is used as the example below. It is not part of BFF.

## 2. Apps

- An App is a first-class object in the Origin, with its own tables and admin page:
    - name, owner, OAuth client (scopes, redirect URLs), webhook endpoint, metadata
      namespace;
    - an App acts as a signed-in user (authorization code with PKCE) or as itself on
      behalf of a tenant (service account, client credentials);
    - every action an App takes is audited under the App's identity and the acting
      user, if any.
- Installing:
    - a tenant administrator installs an App, sees the scopes it asks for, and can
      revoke it at any time;
    - an App built by the tenant itself is installed by its developer.
- Developer role per tenant:
    - creates Apps, rotates keys, sets webhook endpoints;
    - sees webhook delivery logs and API usage for its Apps.
- Scopes are coarse and readable: `files.read`, `files.write`, `shares.manage`,
  `requests.manage`, `members.read`, `members.manage`, `metadata.write`, `events.read`,
  and `tenant.mirror` for Apps that mirror or back up the whole tenant.

## 3. Primitives

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

## 4. Public API

- The one public API of the Origin, `/api/v1`: conventions, authentication, scopes and
  the endpoint list; the chunk-level endpoints are part of it under an "advanced" tag.
- See [Architecture.API.md](Architecture.API.md) for details.

## 5. SDKs and UI packages

- `BigFatFile.Client` (NuGet):
    - typed API from the OpenAPI spec;
    - the transfer engine: chunking, delta, parallel upload with resume, verified
      download, direct-to-storage when offered;
    - auth flows (authorization code with PKCE, device code, client credentials) with a
      pluggable token store;
    - change-feed paging and real-time subscription.
- `@bigfatfile/client` (npm):
    - `BigFatFile.Client` ported for the browser and Node;
    - the chunker and hash function ported into WebAssembly, in a web worker (a
      `worker_thread` on Node);
    - the transfer part is a separate entry point (`@bigfatfile/client/transfer`),
      about 100 to 150 KB, loaded on the first large upload, so an App that only lists
      folders does not pay for it;
    - resumes a half-done upload after a page reload from the session ID and the file's
      fingerprint kept in `localStorage`.
- `@bigfatfile/ui` (npm):
    - Svelte 5 components wrapped as web components:
        - file browser, uploader, share dialog, file-request form;
    - React/Vue/etc. apps embed them with an endpoint and a token;
    - styled by the shared design system, themable by tokens.
- `BigFatFile.Testing` (NuGet):
    - a Testcontainers module that starts a real Origin with a seeded tenant for
      an App's integration tests.
- Python SDK follows when an App needs it:
    - the transfer engine is the part to port, the rest is generated.
- Versioning:
    - SDKs follow semantic versioning;
    - a compatibility matrix states which SDK versions work with which Origin versions;
    - the Origin reports its API version in a header and the SDK checks it on first call.

## 6. The developer experience

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
- `/api/docs` on every instance:
    - the reference docs, generated from the committed spec,
      with "try it" against that instance.
- `docs/developers/`:
    - quickstart: get a token, upload a file, create a file request, receive the
      webhook;
    - one guide per primitive in section 3;
    - SDK guides; an upgrade guide per API version; the changelog.
- `examples/`:
    - small, complete samples: a CI artifact uploader, a backup script, a web page
      embedding the file browser, a file-request portal.
- For module authors (`Architecture.Modules.md`):
    - `dotnet new bff-module`;
    - contract tests against Server.Abstractions;
    - hot reload in the development loop.

## 7. Repository shape

```
sdk/        BigFatFile.Client, BigFatFile.Testing, @bigfatfile/client, @bigfatfile/ui
api/        openapi.json, versioning policy, changelog
apps/       web, desktop, cli, replica            (built on sdk/ only)
server/     the Origin and Server.Abstractions
modules/    open-core modules, the module template
examples/   small, complete samples
docs/
```

## 8. Order

- The platform is build in parallel with the core, not after it:
    - each feature's API is designed in the spec first and reviewed as a diff;
    - the SDK grows with it;
    - the web app is based on the SDK from scratch.
- Day one of a bootable Origin:
    - `bff dev up` and `/api/docs`.
- v2.0 features, because the first external Apps (such as the accounting portal) need
  them:
    - Apps and the developer page;
    - webhooks;
    - file requests;
    - custom metadata;
    - app-managed folders.
- After v2.0:
    - the sandbox tenant;
    - `@bigfatfile/ui` as web components;
    - the Python SDK (?).
