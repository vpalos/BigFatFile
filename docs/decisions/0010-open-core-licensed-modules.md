# Open core licensing

- Status: Accepted
- Recorded: 2026-11-08

## 1. Context

- BigFatFile runs in three deployments from one codebase:
    - the public cloud;
    - dedicated instances for single clients;
    - self-hosted installs by anyone.
- An open core helps the project spread; paid features fund it.

## 2. Decision

- The core is AGPL, and is a complete product for a team.
- Advanced enterprise features, and the features needed to host BigFatFile as a service
  for others, are licensed modules in a private repository.
- The paid modules are called "licensed modules", not "cloud" or "enterprise".

## 3. Alternatives

- No other licensing model is recorded.

## 4. Consequences

- Hosting BigFatFile for yourself and your own organisations is free.
- A module compiles against `Abstractions` from NuGet, and never references another
  module's tables.
- Details: [Platform topology](../architecture/platform/topology.md), section 7, and
  [Licensing](../architecture/platform/licensing.md).
