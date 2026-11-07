# API

The one public API of the Origin: conventions, authentication, scopes and endpoints.
Everything our own Apps and the Replica use is here; there are no private endpoints.

## 1. One API

- One prefix, `/api/v1`, one OpenAPI specification, one version line.
- The transfer endpoints all live under the `nodes` prefix and tag:
    - the file-level ones: create, upload as one stream, download;
    - the chunk-level ones: chunked uploads, manifests, chunks;
    - both are gated by the same `files.*` scopes, except the two endpoints that read
      chunks by hash alone, which take `tenant.mirror` (section 5);
    - what separates the chunk-level ones is an "Advanced" badge on each operation in
      the reference, from the spec's `x-badges` extension; not a prefix, a tag or a
      second specification.
- The committed `openapi.json` is the contract:
    - designed and reviewed as a diff before a feature is implemented;
        - every build of the Origin writes `api/openapi.json` from the code, so an
          endpoint stub is enough to produce the diff;
        - CI fails when the committed file differs from what the build writes;
    - the source of the SDK types;
        - the TypeScript types in `@bigfatfile/client` are generated from it, with the
          same CI check;
    - served by every instance at `/api/docs` with "try it" against that instance.
- Every route of the Origin is mapped in one place:
    - that place makes each route group, with its path, its tag and whether the spec
      includes it;
    - so it shows how the spec is put together, and the modules' routes are added
      there too;
    - a module's routes are under `/api/v1/<module name>`:
        - their tag is the module's name;
        - a module cannot take the name of a core group, such as `account`.
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
    - each error is defined once in the Origin, with its status, code, title and
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
  `/api/v1/account/sign-in/{provider}`:
    - the spec lists the values, as an enum;
    - any other value gets 400 with the code `bad_request`;
    - a test fails when a path, query or header parameter is a plain C# enum, because
      ASP.NET Core would read it by its C# name and would also accept any number.
- Pagination:
    - cursor-based everywhere (`cursor`, `limit`, `nextCursor`); no offsets.
- Idempotency:
    - a node is created with a client-generated UUIDv7, so a retried create returns the
      existing node;
    - every other request that creates something takes an `Idempotency-Key` header, so
      a retried request does not create twice.
- Concurrency:
    - `If-Match` with the node version on every change to a node;
    - a stale version gets `412`.
- Rate limits:
    - per App and per tenant, reported in `RateLimit-*` headers;
    - limits are plan settings;
    - they apply to API calls, not to the transfer endpoints (`content`, `upload`, `chunks`): a slow transfer is a refused product, and abuse is handled by counting
      and ticketing (`Architecture.FileTransfer.md`, section 6).
- Trace context:
    - `traceparent` accepted and propagated.
- Other general conventions:
    - IDs are UUIDv7 strings;
    - times are ISO 8601 in UTC;
    - sizes are bytes.

## 3. Authentication

- Sessions, tokens, CSRF protection and authentication flows are described in detail in
  [Authentication](Architecture.Authentication.md).
- Public endpoints (section 5, `public` tag) don't need tokens.

## 4. Scopes

- Scopes are coarse and readable.
- An App asks for a set; the tenant administrator sees and grants it, or the user does
  for a user-scoped App.
- How scopes work with the session cookie and with the user's role in the tenant is in
  `Architecture.Authentication.md`, section 1, under "Rules".
- Files:
    - `files.read`: list, read metadata, download; manifests and chunks of the nodes
      it may read;
    - `files.write`: create, upload, rename, move, trash, restore; chunked uploads.
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
    - `GET /account/sign-in/{provider}`: starts a Google, Microsoft or OpenID Connect
      sign-in;
    - codes and tokens from emails travel in the request body, never in the path.
- Tenants:
    - `GET /tenants/{id}` (`members.read`): name, plan, settings visible to members;
    - `GET /tenants/{id}/members` (`members.read`), `PATCH .../members/{userId}`,
      `DELETE .../members/{userId}` (`members.manage`);
    - `GET|POST /tenants/{id}/invitations`, `DELETE .../invitations/{invitationId}`
      (`members.manage`); inviting the same address again resends the email;
    - `GET /tenants/{id}/groups` (`members.read`):
        - an admin sees every group;
        - a member sees only the groups they are in;
    - `POST /tenants/{id}/groups`, `PATCH|DELETE .../groups/{groupId}`,
      `PUT .../groups/{groupId}/members` (`members.manage`):
        - a group that comes from a directory gets 409 `group_read_only`.
- Drives:
    - `GET /drives` (`files.read`):
        - the drives the caller is allowed to see, each with its root node and usage;
    - `POST /drives` (`files.write`):
        - a guest gets 403 `member_required`;
    - `PATCH /drives/{id}`, `DELETE /drives/{id}` (`files.write`):
        - need the `owner` role on the drive's root;
        - an admin is the owner of every drive (`Architecture.DataModel.md`, section 4).
- Nodes (files and folders as tree entries):
    - `GET /nodes/{id}` (`files.read`):
        - get the node's metadata, without content or manifest;
        - `?ancestors=true` adds the path ancestors, for breadcrumbs;
    - `GET /nodes/{id}/children` (`files.read`):
        - cursor-paged listing of a folder;
          400 `not_a_folder` for a file;
    - `POST /nodes/resolve` (`files.read`):
        - the node at a path;
        - `drive` and `path` in the body, because paths can be long;
    - `PATCH /nodes/{id}` (`files.write`, `If-Match`):
        - rename, move;
        - the new name and parent are passed in the body;
    - `DELETE /nodes/{id}` (`files.write`, `If-Match`):
        - moves node to trash;
    - `POST /nodes/{id}/restore` (`files.write`):
        - back from trash;
    - `GET /nodes/{id}/versions` (`files.read`):
        - the version history;
    - `POST /nodes/{id}/versions/{v}/restore` (`files.write`):
        - makes an earlier version
          the current one;
    - `POST /nodes/search` (`files.read`):
        - full search in the tree; `q`, `drive`, `path`, `metadata` and the other
          filters are passed in the body;
    - the literal segments (`resolve`, `search`, `chunks`) share the prefix with `{id}`;
        - the ID is routed with a `{id:guid}` constraint, so a literal never binds as an ID.
- Nodes, uploads:
    - The upload ID is actually the node ID:
        - the client gives a UUIDv7 at creation,
        - the `uploads` table is keyed by it, and...
        - the node keeps that ID once it's in the tree (`Architecture.DataModel.md`, section 2).
    - One upload per node at a time:
        - a second start gets 409 unless it resumes the same one.
    - `POST /nodes` (`files.write`):
        - creates a folder, or a file to be uploaded;
        - body: `id` (optional, a client UUIDv7), `parent`, `name`, `kind`, `size` (optional);
        - a repeat with the same `id` returns the existing node;
        - a file is not in the tree until its upload completes;
    - `PUT /nodes/{id}/content` (`files.write`):
        - the simple upload: the bytes as one stream;
        - `Content-Range` to continue from an offset, which needs `size`;
        - the Origin splits the bytes into chunks as they arrive;
        - completes when the received bytes reach `size`, or at the end of the body if no
          size was given (i.e. open-streamed upload);
        - on a node already in the tree it starts a new version and `If-Match` is required;
    - `GET /nodes/{id}/upload` (`files.write`):
        - the resume state:
            - the offset for a simple upload,
            - the missing hashes for a chunked one;
        - 404 when no upload is in progress;
    - `DELETE /nodes/{id}/upload` (`files.write`):
        - abandon upload;
        - a pending new file is discarded;
        - a node already in the tree is left untouched;
    - `GET /nodes/{id}/content` (`files.read`):
        - download with `Range` support;
    - `GET /nodes/{id}/thumbnail` (`files.read`):
        - the thumbnail, if there is one.
- Nodes, chunked uploads (each with the "Advanced" badge):
    - `POST /nodes/{id}/upload` (`files.write`):
        - starts a chunked upload with the manifest in the body;
        - returns the missing hashes;
        - on a node already in the tree it starts a new version and `If-Match` is required;
    - `PUT /nodes/{id}/upload/chunks/{hash}` (`files.write`):
        - one chunk, to a `local` warehouse;
        - access is checked on every call, not just once at start;
    - `POST /nodes/{id}/upload/grants` (`files.write`, S3 warehouses):
        - presigned temporary `PUT` URLs for a batch of up to 64 missing hashes in the body;
        - the SDK asks for the chunks it is about to send, never for the whole file;
        - access is checked on every call;
    - `POST /nodes/{id}/upload/complete` (`files.write`, `If-Match`):
        - in one transaction:
            - for a new file, inserts the node;
            - for a new version, bumps the version;
            - then:
                - commits the manifest,
                - appends the change-log row,
                - deletes the upload;
    - `GET /nodes/{id}/manifest` (`files.read`):
        - the manifest of the node's current version;
        - returns 400 `not_a_file` for a folder;
    - `GET /nodes/{id}/chunks/{hash}` (`files.read`):
        - one chunk of the node's manifest, from a `local` warehouse;
        - a hash outside that manifest is 404;
    - `POST /nodes/{id}/chunks/grants` (`files.read`, S3 warehouses):
        - presigned temporary `GET` URLs for a batch of up to 64 hashes of the node's manifest;
        - asked for in batches as the download proceeds;
    - `GET /nodes/chunks/{hash}` (`tenant.mirror`):
        - any chunk of the tenant, by hash;
    - `POST /nodes/chunks/exists` (`tenant.mirror`):
        - which of the hashes in the body the tenant already has.
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
    - public: `GET /public/requests/{token}`, `POST /public/requests/{token}/nodes`
      (creates a file to be uploaded, as `POST /nodes` does),
      `PUT /public/requests/{token}/nodes/{id}/content`.
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
    - `GET /meta`: API version, server version, the chunking parameter sets it accepts,
      limits, enabled features, and in `account` the sign-up policy, whether password
      sign-in is on, and the external providers;
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
