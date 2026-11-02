# BigFatFile Architecture: Tenancy

Tenants, how their data is kept apart, permissions inside a tenant, and how users and
Apps authenticate.

## 1. Tenants

- A tenant is a company or an individual, with users, groups, drives, a warehouse and
  settings (plan, auth providers);
- One user may belong to several tenants.
- Plans:
    - the open core has one unlimited plan;
    - the billing module enforces storage, bandwidth, retention and feature limits
      through `IPlanPolicy` (`Architecture.PlatformTopology.md`, section 7).

## 2. Tenant isolation

- Application-level scoping (first layer):
    - every tenant-owned table carries `tenant_id`, used to filter all queries;
    - the `/tenants/{id}` endpoints answer 404 unless the ID is the tenant of the session
      or token and the user is still a member of it;
    - a set of isolation tests calls every endpoint with another tenant's IDs and
      expects 404s.
- Row-level security (second layer):
    - two database users:
        - requests connect as the user in `BFF_DATABASE`, which owns nothing, because
          table owners skip row-level security;
        - migrations and the job queue connect as the owner, in `BFF_DATABASE_OWNER`;
        - cross-tenant work (GC, billing, admin tasks) runs as the owner;
        - at startup the Origin creates the request user if it is missing, and grants it
          its rights;
        - the Origin refuses to start when row-level security would not apply to the
          request user;
    - each request has one transaction, which first sets two values:
        - `app.tenant_id`, the tenant of the session or token;
        - `app.user_id`, the signed-in user;
        - both are local to the transaction, so a reused connection never carries them
          into the next request;
        - nothing is saved until the handler commits;
        - the code that runs SQL never opens a connection of its own; it runs on the
          connection it is given, because a query on another connection would run
          without the tenant and the user set;
    - one policy per table, with a simple predicate:
        - `tenant_id = app_tenant_id()`;
        - `app_tenant_id()` and `app_user_id()` read the two values, and return null when
          they are not set;
        - predicates are kept simple so the planner can use the `(tenant_id, …)` indexes;
    - memberships and tenants are also visible to their user, in every tenant:
        - `/api/v1/account/me` lists the user's tenants, and sign-in chooses one of them;
        - this second policy is for reading only, so a membership changes only inside its
          own tenant.

## 3. Permissions

- Rules on nodes grant a role (viewer, editor, owner) to a user or a group;
- The effective permission is the first rule found when walking up the tree from the node;
- External people get access through share links and guest memberships, never through
  anonymous write.
- Sends (transfers to recipients outside the system):
    - nodes under a hidden send root with:
        - an expiry;
        - a download link;
        - an optional password.

## 4. Authentication

- The Origin is the identity provider; no separate auth service.
- Accounts:
    - email and password with verification; external providers (Google, Microsoft,
      GitHub) through standard OAuth;
    - the operator can point the Origin at one OpenID Connect provider (Keycloak,
      Authentik, Entra ID);
    - `BFF_SIGN_UP` sets who may sign up:
        - `invitation`, the default: only invited addresses can create an account;
        - `open`: anyone can, and gets a new tenant of their own; the cloud runs this;
    - `BFF_PASSWORD_SIGN_IN` turns signing in and signing up with a password on or off:
        - `true`, the default;
        - `false`: people sign in only through the external providers, and the password
          endpoints refuse: sign-in, sign-up, forgot password and reset password;
        - `GET /api/v1/meta` reports it in `account.password`;
        - the Origin refuses to start with it off and no external provider set up,
          because nobody could sign in;
    - the operator is the account whose address is `BFF_OPERATOR_EMAIL`, once the
      address is confirmed:
        - the operator can open the job dashboard and create tenants;
        - that address may sign up under either policy;
        - it gets a tenant of its own, so a fresh install has its first tenant;
    - per-tenant identity providers, SAML, SCIM, group sync and LDAP are licensed modules
      behind `IAuthProvider` (`Architecture.PlatformTopology.md`, section 7).
- Sign-in limits:
    - the account lock (`AuthServices.cs`):
        - 10 wrong passwords in a row lock the account for 5 minutes;
        - during the lock, even the right password is refused;
        - a successful sign-in sets the count back to 0;
        - the count has no time window, so wrong passwords a month apart count the same;
        - known issue: anyone who knows an address can keep its account locked,
          by sending 10 wrong passwords every 5 minutes;
    - the limit per IP address (`SignInRateLimit.cs`):
        - one address may make 20 sign-in attempts per minute, for any accounts;
        - more attempts get 429, with a `Retry-After` header;
        - all the addresses of one IPv6 /64 block count as one address;
        - each node counts on its own, so in cluster mode the limit applies per node;
        - behind a reverse proxy, the Origin sees only the proxy's address, so all
          clients share one limit.
- Two-factor sign-in:
    - it is not built yet;
    - password sign-in refuses an account that has it turned on, because Identity's
      password check does not ask for the second step.
- Libraries:
    - ASP.NET Core Identity for accounts (password hashing, email confirmation, lockout,
      two-factor, passkeys);
    - OpenIddict as the OpenID Connect server (authorization code with PKCE, device code,
      refresh tokens, client credentials, JWKS, introspection, revocation);
    - Microsoft's authentication handlers for upstream providers;
    - `Duende.IdentityModel.OidcClient` in desktop clients and the CLI;
    - MailKit for email.
- Flows:
    - web UI: session cookie, same origin, no tokens in the browser;
        - every write operation that uses the cookie carries the `X-CSRF-Token` header;
    - desktop, Replica, CLI: authorization code with PKCE through the system browser and
      a loopback redirect, or device code for headless installs;
    - refresh tokens live in the platform's secure store (DPAPI, keychain, protected
      file).
- Tokens:
    - JWTs signed with an asymmetric key (ES256); public keys published at the JWKS
      endpoint;
    - this is what lets the Replica verify tokens offline; symmetric signing is not used.
    - access tokens are signed and not encrypted, for the same reason;
    - authorization codes and refresh tokens, which only the Origin reads, are signed
      and encrypted (AES);
    - the signing key and the encryption key are created at first start and kept in the
      `server_keys` table, so every node uses the same pair;
        - they are stored unencrypted, like the Data Protection keys: whoever can read
          the database can sign tokens.
- Tenant scope:
    - a session and a token are each scoped to one tenant, in the `tenant_id` claim;
    - sign-in chooses the tenant that the user signed in to last, or else the user's
      oldest membership;
    - a token gets the tenant of the session that authorized it;
    - the session keeps its tenant when the security stamp check rebuilds it;
    - a user with several tenants picks one; the web app switches tenant by asking for a
      new session cookie, so every open tab switches with it;
    - every request finds its tenant in the same place, the signed-in principal.
- The account screens are pages of the web app, which every Origin image serves:
    - the screens are sign-in, sign-up, email confirmation, password reset and
      invitations;
    - the authorization endpoint sends a browser there, and the pages call the
      `/api/v1/account` endpoints (`Architecture.API.md`, section 5);
    - links in emails carry their code after `#`, which browsers never send to a server,
      so the codes stay out of request logs.
- On first sign-in the Origin finds or creates the user, links the external account, and
  provisions tenant membership from invitations or the tenant's domain rules.
- Data Protection keys (cookies, anti-forgery) are stored in the database, so that every
  node uses the same keys and the keys survive a container restart.
- Stores:
    - Identity and OpenIddict are store-based and ship EF Core implementations;
    - one small EF Core `DbContext` holds only their tables and is used at runtime by
      those two libraries; all BigFatFile tables stay on Dapper;
    - the SQL for those tables is generated once from EF and committed as ordinary
      `core_` migration files; EF's own migrations never run;
        - the Server's `print-auth-schema` command prints what the current packages
          expect;
        - a test selects every column the EF model maps; after a package update that
          changes the model, the test fails until a new `core_` file matches it;
    - row-level security does not apply to these tables; users are global and tenant
      membership is ours.
