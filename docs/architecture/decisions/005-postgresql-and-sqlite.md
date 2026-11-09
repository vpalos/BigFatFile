# PostgreSQL and SQLite

- Status: Accepted
- Recorded: 2026-11-08

## 1. Context

- Self-hosting must stay simple.
- The Replica and the desktop client each have one process that uses their database.

## 2. Decision

- The Origin supports PostgreSQL only.
- The Docker Compose deployment includes a PostgreSQL container, so a self-hosted install
  needs nothing else.
- The Replica and the desktop client use SQLite.

## 3. Alternatives

- SQLite as an option on the Origin:
    - the Origin depends on features that only PostgreSQL has:
        - [row-level security](./003-row-level-security.md);
        - advisory locks for migrations in cluster mode, used by Evolve;
        - Hangfire's PostgreSQL storage.

## 4. Consequences

- The server SQL is written for one dialect.
- A SQLite database needs no lock, because one process uses it.
- Details: [Platform topology (section 2)](../platform/topology.md#_2-projects).
