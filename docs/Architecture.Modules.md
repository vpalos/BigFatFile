# BigFatFile Architecture: Modules

Status: design, not yet implemented.

Where the line between the open core and the licensed modules is, how a module plugs
into the server, and the list of modules. Referenced from `Architecture.md`.

## 1. The line

- Open core (AGPL): every project in `Architecture.md` section 2 except the licensed
  modules, including cluster mode, the PostgreSQL notifier, local and S3 warehouses, ClamAV scanning, the
  desktop client, the replica and the CLI. It is a complete product for a team.
- Licensed modules: features an organisation's IT, compliance or finance people ask for,
  and features needed to host BFF as a service for other people.
- Hosting BFF for yourself and your own organisations is free; hosting it as a service
  for others needs the platform modules.

## 2. How a module plugs in

- A module is one assembly that compiles against Server.Abstractions and implements
  `IBffModule`:
    - `Name`, `RequiredCoreVersion`;
    - `ConfigureServices`: registers its services and the core interfaces it implements;
    - `MapEndpoints`: its endpoints under `/api/modules/<name>/`;
    - migrations: SQL files embedded in the assembly, versioned under the module's own
      prefix (for example `sso_0001`), run by Badgie at startup.
- Modules own their tables and endpoints; they reach core data only through core
  services.
- Loading:
    - at startup the server scans `/app/modules/`, loads each assembly in its own
      `AssemblyLoadContext` with Server.Abstractions shared, checks `RequiredCoreVersion`,
      calls `ConfigureServices`, runs the module's migrations, maps its endpoints;
    - the public image has the loader and no modules.
- Licence key:
    - a signed document (Ed25519; the public key is embedded in the server): instance or
      tenant ID, enabled feature keys, seat limit, expiry, grace period;
    - verified offline, so a dedicated instance without internet still starts;
    - `IFeatureGate.IsEnabled("<key>")` is the single check, used by the core (show or hide
      UI, refuse endpoints) and by modules (refuse to activate without their key).
- UI:
    - `/api/ui/manifest` lists enabled features, menu entries and settings pages;
    - the licensed web build includes the modules' UI packages from the private part of
      the workspace; pages appear only when the manifest enables them.
- Versioning: Server.Abstractions uses semantic versioning; a module declares the core
  major it targets and the server refuses to load one built for another.

## 3. Modules

Identity and access:

| Module | Key | Adds | Open core keeps |
|---|---|---|---|
| SSO management | `sso.tenant-config` | per-tenant identity provider settings with an admin UI; domain enforcement (everyone at a domain must use SSO, passwords off) | one server-wide OIDC provider, configured by the operator |
| SAML 2.0 | `sso.saml` | SAML sign-in | OIDC |
| SCIM provisioning | `sso.scim` | the directory pushes user creates, updates and deactivations | invitations, just-in-time provisioning |
| Directory group sync | `sso.groups` | directory groups become BFF groups, kept in sync | manually managed groups |
| LDAP / Active Directory | `sso.ldap` | bind against on-premises AD, including from a dedicated instance inside the client's network | none |
| Access policies | `access.policies` | mandatory 2FA per tenant, IP allowlists, session limits, client version requirements | per-user 2FA and passkeys |

Compliance and governance:

| Module | Key | Adds | Open core keeps |
|---|---|---|---|
| Audit retention and reporting | `audit.extended` | retention beyond the default; search, filter, export; scheduled reports; SIEM export | the audit log with default retention and a basic view |
| Retention and legal hold | `governance.retention` | auto-delete rules, version retention per folder, legal hold, immutable folders with time-limited unlock | trash and the plan's version history |
| Data loss prevention | `governance.dlp` | content rules (CNP, IBAN, card numbers), block or flag sharing, classification labels | none |
| Admin search and discovery | `governance.discovery` | search across the whole tenant for investigations and exports | user-scoped search |

Security:

| Module | Key | Adds | Open core keeps |
|---|---|---|---|
| Advanced threat protection | `security.atp` | commercial scanning engines, shared hash blocklist, quarantine workflow, scan policies | ClamAV scanning on upload |
| Ransomware protection | `security.ransomware` | mass-change detection, honeypot files, automatic sync pause, one-click restore of a drive to a point in time | version history, manual restore of a file or folder |
| Customer-managed keys | `security.byok` | at-rest encryption with keys in the client's key management service | storage-provider encryption |
| Device management | `ops.devices` | enrolled desktop clients per tenant, remote cache wipe, pin policies per group, block unmanaged devices | user-managed pins and devices |

Sharing and branding:

| Module | Key | Adds | Open core keeps |
|---|---|---|---|
| White label and custom domain | `brand.whitelabel` | tenant logo and colours on send and share pages, custom domain, email templates, branded desktop client | BFF branding |
| Sharing policies | `sharing.policies` | tenant-wide rules: external sharing on or off, mandatory passwords and expiry, recipient domain allowlists, link approval | per-link password and expiry |
| Protected viewing | `sharing.protected` | dynamic watermarks, view-only links without download | normal links |

Storage and operations:

| Module | Key | Adds | Open core keeps |
|---|---|---|---|
| Tenant-owned storage | `storage.tenant-warehouses` | warehouses defined per tenant from the web UI (the tenant's own bucket) | operator-defined warehouses from configuration |
| Insights and monitoring | `ops.insights` | usage analytics, storage trends, per-user activity, replica health across many replicas, alerts | per-tenant usage numbers and one replica's status |

Integrations:

| Module | Key | Adds | Open core keeps |
|---|---|---|---|
| Online editing | `integrations.office` | co-editing through Collabora, OnlyOffice or Microsoft 365 (WOPI) | file preview |
| External storage connectors | `integrations.external` | SharePoint, Google Drive and foreign S3 buckets mounted as folders | none |
| Workflow automation | `integrations.automation` | rules (file arrives in X, do Y), approval flows, connectors to automation services | webhooks and the API |

Platform (needed to host BFF as a service):

| Module | Key | Adds | Open core keeps |
|---|---|---|---|
| Billing | `platform.billing` | plans, Stripe, invoices, quota enforcement by plan | one unlimited plan |
| Abuse moderation | `platform.abuse` | report queue, takedown workflow, hash blocklist, rate limits on public sends | the "report abuse" link, which emails the operator |
| Multi-tenant hosting | `platform.hosting` | tenant provisioning, plan assignment, usage metering, support impersonation | multi-tenancy itself; a self-hoster may run several tenants of their own |

## 4. Order

- First release: `platform.billing`, `platform.abuse`, `platform.hosting`, the `sso.*`
  group, `audit.extended`, `brand.whitelabel`.
- All other keys are reserved now so the feature gate is written once.
- One open-core feature (ClamAV scanning) is built as an `IBffModule` compiled into the
  core, to prove the seam before a licensed module depends on it.
