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
    - a set of isolation tests calls every endpoint with another tenant's IDs and
      expects 404s.
- Row-level security (second layer):
    - simple predicates, one policy per table:
        - `tenant_id = current_setting('app.tenant_id', true)::uuid`
    - request handling opens a transaction and first runs:
        - `SET LOCAL app.tenant_id = '<id>'`
        - the setting is local (it ends with the transaction), so a reused connection
          never carries a tenant into the next request;
    - the application connects as a role that does not own the tables, because table
      owners bypass row-level security;
    - cross-tenant work (GC, billing, admin tasks) runs with `BYPASSRLS`;
    - predicates are kept simple so the planner can use the `(tenant_id, …)` indexes.

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
    - the operator is the account whose address is `BFF_OPERATOR_EMAIL`, once the
      address is confirmed:
        - the operator can open the job dashboard and create tenants;
        - that address may sign up under either policy;
        - it gets a tenant of its own, so a fresh install has its first tenant;
    - per-tenant identity providers, SAML, SCIM, group sync and LDAP are licensed modules
      behind `IAuthProvider` (`Architecture.PlatformTopology.md`, section 7).
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
    - a session and a token are each scoped to one tenant, chosen at sign-in;
    - a user with several tenants picks one; the web app switches tenant by asking for a
      new session cookie, so every open tab switches with it;
    - every request finds its tenant in the same place, the signed-in principal.
- The account screens are pages of the web app, which every Origin image serves:
    - the screens are sign-in, sign-up, email confirmation, password reset and
      invitations;
    - the authorization endpoint sends a browser there, and the pages call the
      `/account` endpoints (`Architecture.API.md`, section 5);
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
