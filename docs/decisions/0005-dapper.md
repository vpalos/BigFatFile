# Dapper for data access

- Status: Accepted
- Recorded: 2026-11-08

## 1. Context

- The Origin's SQL must be easy to read and to check, including its row-level security
  behaviour.

## 2. Decision

- The Origin uses Dapper with explicit SQL.
- EF Core stays only where the authentication libraries use it internally.

## 3. Alternatives

- EF Core for the whole Origin:
    - I avoid hidden behaviours, such as EF Core's generators;
    - the SQL that runs would not be visible in the code.

## 4. Consequences

- SQL on the product's tables lives in one model class per concept.
- Details: [Platform topology](../architecture/platform/topology.md), section 2.
