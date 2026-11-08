# Evolve for migrations

- Status: Accepted
- Recorded: 2026-11-08

## 1. Context

- The core and each module own their own tables, and so their own migration scripts.
- Several Origin nodes can start at the same time.
- An older build must keep running during a rolling deploy.

## 2. Decision

- Evolve (NuGet `Evolve`, MIT license) runs the SQL scripts at startup:
    - once per prefix: first the core, then each loaded module;
    - each prefix has its own metadata table, `schema_migrations_<prefix>`.

## 3. Alternatives

- Badgie.Migrator:
    - added complexity.
- A migration runner of our own:
    - added complexity, and more code to maintain.
- EF Core migrations:
    - they do not fit explicit SQL ([Dapper for data access](./0005-dapper.md));
    - EF's own migrations never run.

## 4. Consequences

- Evolve's cluster mode takes a PostgreSQL advisory lock, so each script runs once.
- A changed script that was already applied stops the run.
- Details: [Platform topology](../architecture/platform/topology.md), section 2.
