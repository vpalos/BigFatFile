# Authentication

This document shows:

- how users and Apps sign in and prove who they act for,
- and how each request is authorized.

Users, tenants, tenant isolation and permissions are in [Tenancy](./tenancy.md).

## 1. Core concepts

```mermaid
flowchart TB
    User -->|"member of a tenant
    (admin, member or guest)"| Tenant
    User -->|works through| Client
    Client -->|the Web app uses| Session
    Session -->|makes a token for a client| Token
    Client -->|all other clients use| Token
    Token -->|holds| Scopes
    Session -->|sent with each request| Request
    Scopes -->|sent with each request| Request
```

### Glossary

- **User**:
    - a person with an account, who signs in to prove who they are.
- **Tenant**:
    - the space of an organization or of a person ([Tenancy](./tenancy.md)).
- **Client**:
    - a program that makes requests for the user, such as Web, Desktop etc.;
    - it never sees the user's password;
    - may also act as itself, with no user, inside a tenant (M3).
- **Session**:
    - the proof that the user signed in to the Web app;
    - the browser keeps it in the session cookie.
- **Token**:
    - a proof, signed by the Origin, that lets one client act for one user, in one tenant, for one hour, without the password.
- **Scopes**:
    - actions that the user allowed the client to perform, such as `files.read` or `members.manage`.
- **Request**:
    - an API call, authorized by session or token, the user's role and folder permissions.
- **Claim**:
    - one fact field in a session or a token, such as `sub` (the user's ID) or `tenant_id`
      (the tenant).
- **Principal**:
    - the set of claims that the Origin reads from the cookie or the token, at the start
      of each request.
- **Refresh token**:
    - a value that a client trades for a new access token, without asking the user again.
- **Authorization code**:
    - a one-use value that the Origin gives a client after the user signs in, and that
      the client then trades for tokens.
- **PKCE**:
    - a check used to protect against [code injection](https://oauth.net/2/pkce/) in
      non-web clients (e.g. CLI), when requesting an authorized token:
        - the client generates a secret that only it knows (the "verifier");
        - it sends the hash of the secret (the "challenge") with the authorization request;
        - it receives a code;
        - it then sends the code + the verifier in exchange for a token;
        - this way, the server uses the challenge (from the first request) and the
          verifier to ensure that only that client can finish the authorization flow.

### Who signs in, and why

- A user signs in, to prove who they are:
    - with a password;
    - or with an external account, such as Google or Microsoft;
    - only the Origin ever sees the password.
- A client acts for a signed-in user, and must prove that it's allowed.
    - the web app proves it with the session;
    - every other client proves it with a token.
- What clients do for their user:
    - the web app shows the user's files and settings in the browser;
    - the CLI uploads and downloads files from a shell;
    - the Desktop client "mounts" the tenant's files as a local folder;
    - the Replica keeps a copy of the tenant's files on-premise, which keeps working
      even without internet;
    - any 3rd-party App may do what the user allowed it, such as backing up a folder.
- An App can also act as itself, with no user, but within a tenant (M3):
    - for example, an App that imports files every night;
    - it proves who it is with its own secret, through the client-credentials flow;
    - its token holds the tenant and the scopes, and needs no user.

## 2. Sessions

### How sessions work

- Only the web app uses sessions cookies for authorization:
    - the session cookie lasts 14 days.
- A user who is a member of no tenant gets a session without a tenant:
    - it can read `/api/v1/account/me`;
    - every `/tenants/{id}` endpoint answers 404.
- A user with several tenants can have only one tenant active at a time:
    - the web app switches the tenant by asking for a new session cookie,
      so every open tab switches carries the active tenant;
    - a client can switch to another tenant by getting a new token, but
      this can happen only after the user switches the session to that tenant.
- Some API endpoints (under `/account`) work only with sessions (tokens are not enough), such as:
    - switching the active tenant;
    - accepting an invitation;
    - signing out.
- The session is renewed every 30 minutes by the Origin (the security stamp check):
    - only when the user is active (uses the session);
    - session whose user changed the password since then, is ended;
    - the rebuilt session keeps its tenant.

### CSRF protection

- **CSRF token**:
    - a value that the web app sends with each write request, to prevent
      [CSRF attacks](https://developer.mozilla.org/en-US/docs/Web/Security/Attacks/CSRF)
      (section 2);
    - the web app can always get a new CSRF token from `GET /api/v1/account/csrf`.
- **CSRF flow**:
    - every write request that uses the session cookie must also send the CSRF token;
    - then attaches it to the request in the `X-CSRF-Token` header;
    - another website can't read it, so it can't make the browser send a forged write
      operation with the cookie.

## 3. Tokens and scopes

### Why clients use tokens

- Tokens are used by clients that don't run in a browser:
    - shell, remote server etc.
- Such a client needs another kind of authorization proof (not sessions) because:
    - it can't keep a session cookie;
    - it must not keep the user's password (this would give full access to the account);
- The solution is a token, which gives much less (than a session):
    - one client;
    - one user;
    - one tenant;
    - the scopes that the user allowed;
    - for only one hour.
- Token lifetime:
    - when the token expires, the client trades its refresh token for a new one, without
      asking the user;
    - when the user is deleted, the next refresh fails, and the client loses access.

### Scopes

- A scope is the name of one kind of action, written as `<area><dot><action>`, for example:
    - `files.read`: list folders and download files;
    - `files.write`: upload, rename, move and trash files;
    - `members.manage`: invite and remove members, and change their roles.
- A client lists the scopes that it wants when it asks for a token:
    - the token holds the scopes that it got, in the `scope` claim, separated by spaces;
    - today a client gets every scope that it asks for, from those it is registered with;
    - later, the user or an admin will see the list and agree to it (M3).
- Each endpoint names the one scope that it needs:
    - `GET /tenants/{id}/members` needs `members.read`;
    - `POST /tenants/{id}/invitations` needs `members.manage`;
    - `GET /tenants/{id}/audit` needs `tenant.admin`.
- A scope limits the client, not the user:
    - a token with `members.manage` still fails for a user who is not an admin;
    - the full rule is in section 5, under "Rules".
- The list of scopes, and what each one allows, is in [API](../platform/api.md#_4-scopes).

### Token signing

- The Origin signs each token with its private key (ES256), which it never shares.
- Anyone with the matching public key can check the signature:
    - a token that was changed after signing fails the check;
    - only the private key can make a valid signature, so nobody else can make a token.
- The Origin publishes the public key at `/.well-known/jwks`.
- So the Replica can check tokens without asking the Origin, even while its internet
  connection is down.
- A token is signed but not encrypted, so anyone who holds it can read its claims.

### Token payload

What an access token holds, for example the one that the CLI App gets:

| Claim       | Example                                 | What it means                      |
| ----------- | --------------------------------------- | ---------------------------------- |
| `sub`       | `0192f0c4-…`                            | the user that it acts for          |
| `tenant_id` | `0192f0c5-…`                            | the one tenant that it works in    |
| `client_id` | `bff-dev-cli`                           | the client that holds it           |
| `scope`     | `files.read files.write offline_access` | the actions that the user allowed  |
| `exp`       | `1794013200`                            | when it stops working: in one hour |

- A token also holds the standard claims of a JWT, such as its issuer (`iss`) and the
  time it was issued (`iat`).

### Token types and keys

- **The access token**:
    - a signed JWT, as "Token signing" describes;
    - it holds the user, the App (its client ID), the tenant and the scopes;
    - it lasts one hour.
- **The refresh token**:
    - it is given only with the `offline_access` scope;
    - it lasts 14 days.
- The authorization code lasts 5 minutes, and works once.
- The identity token tells the client who the user is:
    - it holds the user's name only with the `profile` scope, and the email address
      only with the `email` scope;
    - the API does not accept it.
- Authorization codes and refresh tokens are read only by the Origin, so they are
  signed and also encrypted (AES).
- The lifetimes are OpenIddict's defaults, which the Origin does not change.
- **Keys**:
    - the signing key and the encryption key are created at first start, and kept in
      the `server_keys` table, so every node uses the same pair;
    - they are stored unencrypted, like the Data Protection keys: whoever can read the
      database can sign tokens.

## 4. Sign-in flows

### Sign-in methods

- The Origin is the identity provider. There is no separate auth service.
- A password works only once the email address is confirmed.
- `BFF_PASSWORD_SIGN_IN` turns signing in and signing up with a password on or off:
    - `true`, the default;
    - `false`: people sign in only through the external providers, and the password
      endpoints refuse: sign-in, sign-up, forgot password and reset password;
    - `GET /api/v1/meta` reports it in `account.password`;
    - the Origin refuses to start with it off and no external provider set up,
      because nobody could sign in.
- Who may sign up, and the operator's account, are in [Tenancy](./tenancy.md#_2-accounts).
- Per-tenant identity providers, SAML, SCIM, group sync and LDAP are licensed modules
  behind `IAuthProvider` ([Topology (section 7)](../platform/topology.md#_7-licensed-modules)).

### Web app sign-in using email and password

```mermaid
sequenceDiagram
    participant B as Browser (web app)
    participant O as Origin
    B->>O: POST /api/v1/account/sign-in<br/>the email address and the password, and optionally a tenantId
    alt over 20 attempts in one minute from the IP address
        O->>B: 429 too_many_requests
    else wrong email or password, locked account, or unconfirmed email
        Note over O: writes sign_in_failed to the audit log, when the account exists<br/>10 wrong passwords in a row lock the account for 5 minutes
        O->>B: 401 wrong_credentials, 403 locked_out or 403 email_not_confirmed
    else right password
        Note over O: chooses the tenant: the tenantId sent, or else the last one signed in to,<br/>or else the oldest membership<br/>writes signed_in to the audit log, in that tenant<br/>starts the session, for the user in that tenant
        O->>B: 204, with the session cookie
        B->>O: the cookie, with every later request
    end
```

### Web app sign-in using an external account (OAuth)

A user can sign in with an account at another service, instead of a password. The Origin
asks that service, through OAuth and OpenID Connect, who the user is.

```mermaid
sequenceDiagram
    participant B as Browser (web app)
    participant O as Origin
    participant P as Provider
    B->>O: opens GET /api/v1/account/sign-in/google?returnUrl=…<br/>as a page, not with a fetch call
    O->>B: a redirect to the provider's sign-in page,<br/>with the Origin's client ID
    B->>P: the provider's sign-in page
    Note over P: the user signs in there<br/>and agrees to share their email address and name
    P->>B: a redirect to the Origin, with a code
    B->>O: the code
    O->>P: the code, and the Origin's client secret
    P->>O: the user's ID at the provider, email address and name,<br/>and whether the provider has verified the address
    alt the address is not verified
        O->>B: the sign-in is refused
    else the address is verified
        Note over O: finds the user linked to that account<br/>on the first sign-in: finds the user with that address, or creates one,<br/>then links the account<br/>chooses the tenant and writes signed_in, as for a password sign-in<br/>starts the session
        O->>B: a redirect to returnUrl, with the session cookie
    end
```

- The providers:
    - Google;
    - Microsoft;
    - one OpenID Connect provider that the operator sets up, such as Keycloak, Authentik
      or Entra ID.
- `GET /api/v1/meta` lists the providers that this Origin has set up, in
  `account.providers`.
- The provider must have verified the email address:
    - the Origin refuses the sign-in when the provider does not say that it has;
    - without this rule, someone could create an account at a provider with another
      person's address, and then sign in to that person's account here, or accept their
      invitations.
- On the first sign-in:
    - the Origin finds the user with the same email address, or creates one when
      `BFF_SIGN_UP` allows it;
    - it links the external account to the user, and later sign-ins find the user by
      that link;
    - it adds the user to tenants from their invitations, or from a tenant's domain
      rules.

### How an App is authorized

```mermaid
sequenceDiagram
    participant C as CLI
    participant B as Browser
    participant O as Origin
    C->>B: opens the system browser<br/>at /connect/authorize
    Note over C,B: with its client ID and the scopes it wants<br/>a PKCE challenge, the hash of a secret<br/>that only the CLI knows<br/>its loopback address, such as<br/>http://127.0.0.1:53682/callback
    B->>O: /connect/authorize,<br/>with the session cookie
    Note over O: no session: sends the browser to the web app's<br/>sign-in screen first, which signs the user in<br/>with a password or an external account, as above<br/>an App from someone else: shows the consent<br/>screen, where the user agrees to the scopes
    O->>B: a redirect to the loopback address,<br/>with an authorization code
    Note over O: the code holds the user,<br/>the session's tenant and the scopes
    B->>C: the authorization code,<br/>at the loopback address
    C->>O: the code and the PKCE secret, to /connect/token
    Note over O: checks that the secret<br/>matches the challenge<br/>checks that the user still exists<br/>and is not locked<br/>writes app_authorized to the audit log
    O->>C: an access token, and a refresh token<br/>with offline_access
    Note over C,O: the CLI sends the access token with each request<br/>when it expires, the CLI trades the refresh token for a new one
```

- A refresh writes no audit row, because it only renews what the client already has.
- The refresh makes the same check:
    - a deleted user's client loses access when its access token expires;
    - a locked user's client cannot refresh until the lock ends.
- First-party clients are registered with implicit consent, so their users see no
  consent screen.
- The consent screen is not built yet, so an App from someone else is refused.

### Client flows

How each client gets its proof:

- The web app:
    - it is served by the Origin, on the same origin as the API;
    - it uses the session cookie, so the browser holds no tokens.
- The desktop client, the Replica and the CLI:
    - they get tokens through the authorization code flow with PKCE, in the system
      browser, with a loopback redirect;
    - they keep the refresh token in the platform's secure store (DPAPI, keychain,
      protected file);
    - headless installs will use the device code flow (not built yet).
- Apps:
    - an App that acts for a user gets tokens the same way as the CLI;
    - an App that acts as itself uses the client-credentials flow, as "Who signs in, and
      why" says (M3);
    - an App that acts for a user later, with both identities in the token, will use
      token exchange (RFC 8693) (M4).

## 5. Requests

```mermaid
flowchart TB
    Request["`**Request**
    has an Authorization: Bearer header?`"]
    Token["the Origin checks the token's signature and expiry"]
    Cookie["the Origin reads the session cookie"]
    Principal["`**Principal**
    the user and the tenant, and a token's scopes`"]
    Scope["`**Scope**
    the endpoint names one scope:
    a token must hold it
    a session passes, except for tenant.mirror`"]
    CSRF["`**CSRF**
    a write operation that uses the cookie must send the X-CSRF-Token header`"]
    Tenant["`**Tenant**
    for /tenants/{id} only:
    the ID must be the principal's tenant
    the user must still be a member of it
    otherwise 404, the same answer as for a tenant that does not exist
    the role must allow the endpoint's scope`"]
    Database["`**Database**
    one transaction, with app.tenant_id and app.user_id set
    row-level security shows only the tenant's rows (Tenancy, section 4)`"]

    Request -->|yes| Token
    Request -->|no| Cookie
    Token --> Principal
    Cookie --> Principal
    Principal --> Scope --> CSRF --> Tenant --> Database
```

### Rules

- A session and a token each work for one user, in one tenant:
    - the tenant is in the `tenant_id` claim;
    - a token gets the tenant of the session that let the client get it;
    - a refreshed token keeps its tenant and its scopes;
    - every request finds its tenant in the same place, the principal.
- The role is in neither the session nor the token:
    - the Origin reads it from the membership on every request;
    - so that a change of role, or a removal from the tenant, is applied immediately.
- A request is allowed only if allowed by all of these:
    - the scope, which contains the claims that the user let the client perform:
        - a token must hold the scope that the endpoint names;
        - a session holds no scopes, and may do all that its user may do;
        - the one exception is `tenant.mirror`, which only a token can hold (it's for Apps);
    - the role, which represents what the user may do within the tenant:
        - `members.manage` and `tenant.admin` need an admin:
            - others get 403 with the error code `admin_required`;
        - `members.read` refuses a guest:
            - a guest gets 403 with the code `member_required`;
        - both rules apply with a token and with the session;
    - the permission on the folder acted upon ([Tenancy](./tenancy.md#_5-permissions)).

## 6. Implementation details

### Sign-in limits

- The account lock (`UserServices.cs`):
    - 10 wrong passwords in a row lock the account for 5 minutes;
    - during the lock, even the right password is refused;
    - during the lock, the token endpoint also refuses the user's codes and refresh
      tokens;
    - a successful sign-in sets the count back to 0;
    - the count has no time window, so wrong passwords a month apart count the same;
    - known issue: anyone who knows an address can keep its account locked, by sending
      10 wrong passwords every 5 minutes. This also stops the user's CLI and Apps from
      refreshing their tokens.
- The limit per IP address (`SignInRateLimit.cs`):
    - one address may make 20 sign-in attempts per minute, for any accounts;
    - more attempts get 429, with a `Retry-After` header;
    - all the addresses of one IPv6 /64 block count as one address;
    - each node counts on its own, so in cluster mode the limit applies per node;
    - behind a reverse proxy, the Origin sees only the proxy's address, so all clients
      share one limit.

### Two-factor sign-in

- It is not built yet.
- Password sign-in refuses an account that has it turned on, because Identity's password
  check does not ask for the second step.

### Account screens

- The account screens are pages of the web app, which every Origin image serves:
    - the screens are sign-in, sign-up, email confirmation, password reset and
      invitations;
    - the authorization endpoint sends a browser there, and the pages call the
      `/api/v1/account` endpoints ([API](../platform/api.md), section 5);
    - links in emails carry their code after `#`, which browsers never send to a server,
      so the codes stay out of request logs.

### Libraries and stores

- Libraries:
    - ASP.NET Core Identity for accounts (password hashing, email confirmation, lockout,
      two-factor, passkeys);
    - OpenIddict as the OpenID Connect server (authorization code with PKCE, device code,
      refresh tokens, client credentials, JWKS, introspection, revocation);
    - Microsoft's authentication handlers for upstream providers;
    - `Duende.IdentityModel.OidcClient` in desktop clients and the CLI;
    - MailKit for email.
- Data Protection keys (cookies, anti-forgery) are stored in the database, so that every
  node uses the same keys and the keys survive a container restart.
- Stores:
    - Identity and OpenIddict are store-based and ship EF Core implementations;
    - one small EF Core `DbContext` holds only their tables and is used at runtime by
      those two libraries; all BigFatFile tables stay on Dapper;
    - the SQL for those tables is generated once from EF and committed as ordinary
      core scripts; EF's own migrations never run;
        - the Origin's `print-auth-schema` command prints what the current packages
          expect;
        - a test selects every column the EF model maps; after a package update that
          changes the model, the test fails until a new core script matches it;
    - row-level security does not apply to these tables; users are global and tenant
      membership is ours.
