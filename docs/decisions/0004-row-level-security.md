# Row-level security

- Status: Accepted
- Recorded: 2026-11-08

## 1. Context

- Every tenant-owned table carries `tenant_id`, and every query filters on it.
- One query that forgets the filter would show another tenant's data.

## 2. Decision

- Tenant isolation has two layers:
    - application-level scoping, checked by tests that call every endpoint with another
      tenant's IDs;
    - PostgreSQL row-level security, with one simple policy per table.

## 3. Alternatives

- Application-level scoping only:
    - a single missing filter is a data leak, and nothing in the database stops it.

## 4. Consequences

- Requests connect as a database user that owns nothing, because table owners skip
  row-level security.
- Each request runs in one transaction that first sets the tenant and the user.
- Code that runs SQL uses only the connection it is given.
- Partitions of a partitioned table are reachable only through the parent table.
- Details: [Authentication](../architecture/security/authentication.md), section 4.
