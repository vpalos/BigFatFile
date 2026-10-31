# BigFatFile Architecture: API

The one public API of the Origin: conventions, authentication, scopes and endpoints.
Everything our own Apps and the Replica use is here; there are no private endpoints.

## 1. One API

- One prefix, `/api/v1`, one OpenAPI specification, one version line.
- The chunk-level endpoints (upload sessions, manifests, chunks) are ordinary endpoints
  under the `transfer` tag, marked "advanced" in the reference. They are gated by the
  same `files.*` scopes as the file-level endpoints; what separates them is a tag, not
  a prefix or a second specification.
- The committed `openapi.json` is the contract:
    - designed and reviewed as a diff before a feature is implemented;
        - every build of the Server writes `api/openapi.json` from the code, so an
          endpoint stub is enough to produce the diff;
        - CI fails when the committed file differs from what the build writes;
    - the source of the SDK types;
        - the TypeScript types in `@bigfatfile/client` are generated from it, with the
          same CI check;
    - served by every instance at `/api/docs` with "try it" against that instance.
- Tags group the endpoints: `auth`, `tenant`, `drives`, `nodes`, `files`, `transfer`,
  `changes`, `permissions`, `shares`, `sends`, `requests`, `metadata`, `apps`,
  `webhooks`, `admin`, `mirror`, `public`, `meta`.

## 2. Conventions

- Versioning:
    - `/api/v1`; a new major version runs beside the previous one while deprecated;
    - deprecations are announced in the changelog and in a `Deprecation` response
      header.
- Errors: problem details (RFC 9457), with a stable `type` per error and a
  machine-readable `code`.
- Pagination: cursor-based everywhere (`cursor`, `limit`, `nextCursor`); no offsets.
- Writes: an `Idempotency-Key` header on every create, so a retried request does not
  create twice.
- Concurrency: `If-Match` with the node version on every change to a node; a stale
  version gets `412`.
- Rate limits: per App and per tenant, reported in `RateLimit-*` headers; limits are
  plan settings.
- Trace context: `traceparent` accepted and propagated.
- IDs are UUIDv7 strings; times are ISO 8601 in UTC; sizes are bytes.

## 3. Authentication

- Bearer JWT (ES256, keys at the JWKS endpoint) issued by the Origin's OpenIddict
  server; the web app on the same origin uses a session cookie instead.
- A token carries: the user (if any), the App, the tenant it is scoped to, the granted
  scopes.
- Flows:
    - authorization code with PKCE: desktop, CLI, third-party Apps acting for a user;
    - device code: headless installs;
    - client credentials: an App acting as itself for a tenant (service account);
    - refresh tokens with `offline_access`;
    - token exchange (RFC 8693): an App acting on behalf of a user later, with both
      identities in the token.
- Public endpoints (section 5, `public` tag) need no token: share links, send downloads,
  file-request uploads; a link token in the path, and the link's own password where set.

## 4. Scopes

- Scopes are coarse and readable.
- An App asks for a set; the tenant administrator (or the user, for user-scoped Apps) sees and grants it.
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
- Me and tenants:
    - `GET /me` (`openid`): the user, their tenants and roles;
    - `GET /tenants/{id}` (`members.read`): name, plan, settings visible to members;
    - `GET /tenants/{id}/members`, `POST .../members/invite`,
      `PATCH .../members/{userId}`, `DELETE .../members/{userId}` (`members.*`);
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
    - `POST /nodes` (`files.write`): create a folder;
    - `PATCH /nodes/{id}` (`files.write`, `If-Match`): rename, move;
    - `DELETE /nodes/{id}` (`files.write`, `If-Match`): to trash;
      `POST /nodes/{id}/restore`;
    - `GET /nodes/{id}/versions`, `POST /nodes/{id}/versions/{v}/restore`
      (`files.read` / `files.write`);
    - `GET /search?q=&drive=&metadata=` (`files.read`).
- Files, file-level transfer (`files` tag):
    - `POST /files` (`files.write`): name, parent, size; returns an upload ID;
    - `PUT /files/{uploadId}/content` (`files.write`): the bytes as one stream;
      `Content-Range` to continue from an offset; the Origin chunks inline;
    - `GET /files/{uploadId}` (`files.write`): the offset to resume from;
    - `GET /nodes/{id}/content` (`files.read`): download with `Range` support;
    - `GET /nodes/{id}/thumbnail`, `GET /nodes/{id}/preview` (`files.read`).
- Transfer, chunk-level (`transfer` tag, advanced):
    - `POST /transfer/sessions` (`files.write`): the manifest for a node; returns the
      missing chunk hashes and, on S3 warehouses, a presigned URL per chunk;
    - `GET /transfer/sessions/{id}` (`files.write`): what is still missing;
    - `PUT /transfer/sessions/{id}/chunks/{hash}` (`files.write`): one chunk;
    - `POST /transfer/sessions/{id}/complete` (`files.write`, `If-Match`);
    - `DELETE /transfer/sessions/{id}` (`files.write`): abandon;
    - `GET /nodes/{id}/manifest` (`files.read`): the manifest with a grant (or presigned
      URL) per chunk;
    - `GET /transfer/chunks/{hash}` (`files.read` with a grant, or `tenant.mirror`);
    - `POST /transfer/chunks/exists` (`files.read` with grants, or `tenant.mirror`):
      which of these hashes the tenant has.
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
    - the change feed, `GET /nodes/{id}/versions` and the `transfer` endpoints, used with
      this scope.
- Meta (no scope):
    - `GET /meta`: API version, server version, chunker parameters, limits, enabled
      features;
    - `GET /openapi.json`;
    - `GET /api/docs` (outside `/api/v1`);
    - `GET /health` (outside `/api/v1`): `status` (`healthy`, `degraded` or `unhealthy`)
      and `timestamp`, in JSON; 503 when a check fails, with the failing check named
      only in the log.

## 6. What the SDKs add on top

- `files.upload(folder, input)`: a seekable input of known size goes through the
  `transfer` endpoints (delta, deduplication, parallel upload); a stream of unknown
  length goes through `POST /files`. The App's call is the same either way.
- `files.download(id)`: assembles from chunks where the runtime can stream to disk;
  otherwise `GET /nodes/{id}/content`.
- Pagination, retries, `Idempotency-Key`, `If-Match` and token refresh are handled by
  the SDK; an App that calls the API directly handles them itself, as documented here.
