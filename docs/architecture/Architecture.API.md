# BigFatFile Architecture: API

The one public API of the Origin: conventions, authentication, scopes and endpoints.
Everything our own Apps and the Replica use is here; there are no private endpoints.

## 1. One API

- One prefix, `/api/v1`, one OpenAPI specification, one version line.
- The transfer endpoints all live under the `nodes` prefix and tag:
    - the file-level ones: create, upload as one stream, download;
    - the chunk-level ones: upload sessions, manifests, chunks;
    - both are gated by the same `files.*` scopes;
    - what separates the chunk-level ones is an "Advanced" badge on each operation in
      the reference, from the spec's `x-badges` extension; not a prefix, a tag or a
      second specification.
- The committed `openapi.json` is the contract:
    - designed and reviewed as a diff before a feature is implemented;
        - every build of the Server writes `api/openapi.json` from the code, so an
          endpoint stub is enough to produce the diff;
        - CI fails when the committed file differs from what the build writes;
    - the source of the SDK types;
        - the TypeScript types in `@bigfatfile/client` are generated from it, with the
          same CI check;
    - served by every instance at `/api/docs` with "try it" against that instance.
- Every route of the Origin is mapped in one place in the Server:
    - that place makes each route group, with its path, its tag and whether the spec
      includes it;
    - so it shows how the spec is put together, and the modules' routes are added
      there too.
- Tags group the endpoints:
    - an endpoint's tag is the first segment of its path after `/api/v1`:
      `/api/v1/account/me` is `account`, `/api/v1/tenants/{id}` is `tenants`;
    - each group in the endpoint map sets its tag, and no endpoint sets its own;
    - a test fails when a tag is not the first segment of its path, and another when a
      path of the spec is outside `/api/v1`.

## 2. Conventions

- Versioning:
    - `/api/v1`; a new major version runs beside the previous one while the previous
      one is deprecated;
    - deprecations are announced in the changelog and in a `Deprecation` response
      header.
- Errors are problem details (RFC 9457):
    - the fields of the body:
        - `type`, a URN that identifies the error: `urn:bigfatfile:problem:<code>`;
        - `title`, a short text for people, which a client never compares;
        - `status`, the HTTP status;
        - `code`, which a client branches on;
        - `traceId`, the ID of the request's trace;
        - `errors`, only for field errors;
    - each error is defined once in the Server, with its status, code, title and
      description:
        - the spec's example bodies and its list of every error are built from these
          definitions;
        - a test fails when a code has no definition;
    - each endpoint declares the errors that it returns, with a clause that says when;
    - the spec lists an endpoint's errors under each response, with one example body per
      code;
    - an error that ASP.NET Core writes without a code, such as a 404 for an unknown
      address, gets the generic code for its status.
- Parameters with a fixed set of values, such as the provider in
  `/api/v1/account/external/{provider}`:
    - the spec lists the values, as an enum;
    - any other value gets 400 with the code `bad_request`;
    - a test fails when a path, query or header parameter is a plain C# enum, because
      ASP.NET Core would read it by its C# name and would also accept any number.
- Pagination:
    - cursor-based everywhere (`cursor`, `limit`, `nextCursor`); no offsets.
- Idempotency:
    - an `Idempotency-Key` header on every request that creates something;
    - this prevents a retried request from creating something twice.
- Concurrency:
    - `If-Match` with the node version on every change to a node;
    - a stale version gets `412`.
- Rate limits:
    - per App and per tenant, reported in `RateLimit-*` headers;
    - limits are plan settings.
- Trace context:
    - `traceparent` accepted and propagated.
- Other general conventions:
    - IDs are UUIDv7 strings;
    - times are ISO 8601 in UTC;
    - sizes are bytes.

## 3. Authentication

- Bearer JWT (ES256, keys at the JWKS endpoint) issued by the Origin's OpenIddict
  server; the web app on the same origin uses a session cookie instead.
- Every write operation that uses the session cookie sends the `X-CSRF-Token` header,
  with the token from `GET /api/v1/account/csrf`; bearer requests do not need it.
- A token carries: the user (if any), the App, the tenant it is scoped to, the granted
  scopes.
- Flows:
    - authorization code with PKCE: desktop, CLI, third-party Apps acting for a user;
    - device code: headless installs;
    - client credentials: an App acting as itself for a tenant (service account);
    - refresh tokens with `offline_access`;
    - token exchange (RFC 8693): an App acting on behalf of a user later, with both
      identities in the token.
- Public endpoints (section 5, `public` tag) need no token: share links, downloads of
  sends, uploads to file requests; a link token in the path, and the link's own password
  where set.

## 4. Scopes

- Scopes are coarse and readable.
- An App asks for a set; the tenant administrator sees and grants it, or the user does
  for a user-scoped App.
- Files:
    - `files.read`: list, read metadata, download; manifests and chunk grants;
    - `files.write`: create, upload, rename, move, trash, restore; upload sessions.
- Sharing:
    - `shares.manage`: create and revoke share links on nodes the App may read;
    - `sends.manage`: create sends;
    - `requests.manage`: create and manage file requests.
- People:
    - `members.read`: list members and groups;
    - `members.manage`: invite, remove, change roles, manage groups.
- Data about files:
    - `metadata.read`, `metadata.write`: the App's own metadata namespace;
    - `permissions.manage`: change permission rules on nodes.
- Events:
    - `events.read`: the change feed and the real-time hub;
    - `webhooks.manage`: subscriptions for the App.
- Platform:
    - `apps.manage`: the developer role: create Apps, rotate keys, see deliveries and
      usage;
    - `tenant.admin`: tenant settings, warehouses, replicas, audit, usage;
    - `tenant.mirror`: every chunk and the whole change feed of the tenant, for mirroring
      and backup Apps; the Replica holds it.
- Standard OpenID Connect scopes: `openid`, `profile`, `email`, `offline_access`.

## 5. Endpoints

Paths are relative to `/api/v1`. The scope in brackets is the one required; node-level
permissions apply on top.

- Auth (OpenIddict, standard paths, outside `/api/v1`):
    - `GET /.well-known/openid-configuration`, `GET /.well-known/jwks`;
    - `GET /connect/authorize`, `POST /connect/token` (all grant types above),
      `POST /connect/revoke`, `POST /connect/introspect`;
    - `GET /connect/userinfo`.
- Account (`account` tag):
    - `GET /account/me` (`openid`): the user, the tenant the caller is scoped to, all
      their tenants and roles; it accepts a token or the session;
    - the other endpoints are the JSON endpoints behind the web app's account screens;
      same origin and session cookie only, no scope;
    - `GET /account/csrf`: the token for the `X-CSRF-Token` header;
    - `POST /account/sign-in`, `POST /account/sign-out`;
    - `PUT /account/tenant`: scopes the session to another of the user's tenants;
    - `POST /account/sign-up`, `POST /account/confirm-email`,
      `POST /account/resend-confirmation`;
    - `POST /account/forgot-password`, `POST /account/reset-password`;
    - `POST /account/invitations/lookup`, `POST /account/invitations/accept`;
    - `GET /account/external/{provider}`: starts a Google, Microsoft or OpenID Connect
      sign-in;
    - codes and tokens from emails travel in the request body, never in the path.
- Tenants:
    - `GET /tenants/{id}` (`members.read`): name, plan, settings visible to members;
    - `GET /tenants/{id}/members` (`members.read`), `PATCH .../members/{userId}`,
      `DELETE .../members/{userId}` (`members.manage`);
    - `GET|POST /tenants/{id}/invitations`, `DELETE .../invitations/{invitationId}`
      (`members.manage`); inviting the same address again resends the email;
    - `GET|POST /tenants/{id}/groups`, `PATCH|DELETE .../groups/{groupId}`,
      `PUT .../groups/{groupId}/members` (`members.manage`).
- Drives:
    - `GET /drives` (`files.read`): the drives the caller may see, each with its root
      node and usage;
    - `POST /drives`, `PATCH /drives/{id}`, `DELETE /drives/{id}` (`tenant.admin`).
- Nodes (files and folders as tree entries):
    - `GET /nodes/{id}` (`files.read`);
    - `GET /nodes/{id}/children` (`files.read`): cursor-paged listing;
    - `GET /nodes/{id}/path` (`files.read`): the ancestors, for breadcrumbs;
    - `GET /nodes/by-path?drive=&path=` (`files.read`);
    - `PATCH /nodes/{id}` (`files.write`, `If-Match`): rename, move;
    - `DELETE /nodes/{id}` (`files.write`, `If-Match`): to trash;
      `POST /nodes/{id}/restore`;
    - `GET /nodes/{id}/versions`, `POST /nodes/{id}/versions/{v}/restore`
      (`files.read` / `files.write`);
    - `GET /search?q=&drive=&metadata=` (`files.read`).
- Nodes, file-level transfer:
    - `POST /nodes` (`files.write`): creates a folder, or a file from its name, parent
      and size; for a file it returns an upload ID;
    - `PUT /nodes/uploads/{uploadId}/content` (`files.write`): the bytes as one stream;
      `Content-Range` to continue from an offset; the Origin splits the bytes into chunks
      as they arrive;
    - `GET /nodes/uploads/{uploadId}` (`files.write`): the offset to resume from;
    - `GET /nodes/{id}/content` (`files.read`): download with `Range` support;
    - `GET /nodes/{id}/thumbnail`, `GET /nodes/{id}/preview` (`files.read`).
- Nodes, chunk-level transfer (each with the "Advanced" badge):
    - `POST /nodes/sessions` (`files.write`): the manifest for a node; returns the
      missing chunk hashes and, on S3 warehouses, a presigned URL per chunk;
    - `GET /nodes/sessions/{id}` (`files.write`): what is still missing;
    - `PUT /nodes/sessions/{id}/chunks/{hash}` (`files.write`): one chunk;
    - `POST /nodes/sessions/{id}/complete` (`files.write`, `If-Match`);
    - `DELETE /nodes/sessions/{id}` (`files.write`): abandon;
    - `GET /nodes/{id}/manifest` (`files.read`): the manifest with a grant (or presigned
      URL) per chunk;
    - `GET /nodes/chunks/{hash}` (`files.read` with a grant, or `tenant.mirror`);
    - `POST /nodes/chunks/exists` (`files.read` with grants, or `tenant.mirror`):
      which of these chunks the tenant already has.
- Changes:
    - `GET /changes?since=&limit=&subtree=` (`events.read`): the change log from a
      cursor; `subtree` limits it to a folder;
    - `/hub` (`events.read`): the SignalR hub; `ChangesAvailable(tenantId, latestSeq)`.
- Permissions:
    - `GET /nodes/{id}/permissions` (`files.read`): effective and explicit rules;
    - `PUT /nodes/{id}/permissions` (`permissions.manage`).
- Shares (links to drive content):
    - `GET|POST /shares` (`shares.manage`), `GET|PATCH|DELETE /shares/{id}`;
    - public: `GET /public/shares/{token}`, `GET /public/shares/{token}/content`,
      `POST /public/shares/{token}/unlock` (password).
- Sends (transfers to recipients outside the system):
    - `POST /sends` (`sends.manage`): files with sizes, recipients, expiry, password;
      returns per-file upload targets (presigned multipart on S3, `PUT` on local);
    - `PUT /sends/{id}/files/{fileId}/content` (local warehouses);
    - `POST /sends/{id}/complete`, `GET /sends`, `GET /sends/{id}`, `DELETE /sends/{id}`;
    - public: `GET /public/sends/{token}`, `GET /public/sends/{token}/content`,
      `GET /public/sends/{token}/files/{fileId}/content`,
      `POST /public/sends/{token}/report` (abuse).
- File requests (uploads into a folder by people without an account):
    - `GET|POST /requests` (`requests.manage`), `GET|PATCH|DELETE /requests/{id}`;
    - public: `GET /public/requests/{token}`, `POST /public/requests/{token}/files`
      (starts an upload), `PUT /public/requests/{token}/files/{uploadId}/content`.
- Metadata:
    - `GET /nodes/{id}/metadata` (`metadata.read`): all namespaces the App may read;
    - `PUT|DELETE /nodes/{id}/metadata/{namespace}` (`metadata.write`): the App's own
      namespace only.
- Apps and webhooks (developer):
    - `GET|POST /apps` (`apps.manage`), `GET|PATCH|DELETE /apps/{id}`;
    - `POST /apps/{id}/secrets`, `DELETE /apps/{id}/secrets/{secretId}`;
    - `GET|POST /apps/{id}/webhooks` (`webhooks.manage`), `DELETE .../webhooks/{hookId}`;
    - `GET /apps/{id}/deliveries`, `POST /apps/{id}/deliveries/{deliveryId}/replay`;
    - `GET /apps/{id}/usage`;
    - install for a tenant: `GET /tenants/{id}/apps`, `POST /tenants/{id}/apps/{appId}`
      (grant scopes), `DELETE /tenants/{id}/apps/{appId}` (`tenant.admin`).
- Admin (`tenant.admin`):
    - `GET|PATCH /tenants/{id}/settings`;
    - `GET /tenants/{id}/usage`, `GET /tenants/{id}/audit?from=&to=&actor=`;
    - `GET|POST /tenants/{id}/replicas`, `GET|PATCH|DELETE .../replicas/{replicaId}`:
      pairing code, LAN address and public key, status;
    - `GET|POST /tenants/{id}/warehouses` (licensed module);
    - `GET /tenants/{id}/features`: what the licence enables.
- Mirror (`tenant.mirror`, advanced):
    - `GET /snapshot?drive=` : the tree with every node's current version and manifest
      ID, the same format as the daily warehouse snapshot;
    - `PUT /replicas/{id}/status`: lag, completeness, last verification (the Replica
      reports about itself);
    - the change feed, `GET /nodes/{id}/versions` and the chunk-level `nodes` endpoints,
      used with this scope.
- Meta (no scope):
    - `GET /meta`: API version, server version, chunker parameters, limits, enabled
      features, and in `account` the sign-up policy, whether password sign-in is on, and
      the external providers;
    - `GET /openapi.json`;
    - `GET /api/docs` (outside `/api/v1`);
    - `GET /health` (outside `/api/v1`): `status` (`healthy`, `degraded` or `unhealthy`)
      and `timestamp`, in JSON; 503 when a check fails, with the failing check named
      only in the log.

## 6. What the SDKs add on top

- `files.upload(folder, input)`: a seekable input of known size goes through the
  chunk-level endpoints (delta, deduplication, parallel upload); a stream of unknown
  length goes through the file-level upload. The App's call is the same either way.
- `files.download(id)`: assembles the file from its chunks when the runtime can stream to
  disk; otherwise `GET /nodes/{id}/content`.
- Pagination, retries, `Idempotency-Key`, `If-Match` and token refresh are handled by
  the SDK; an App that calls the API directly handles them itself, as documented here.
