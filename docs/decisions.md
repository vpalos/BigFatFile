# Decisions

A decision record keeps the reasons for a major decision, and the options that were turned
down.

## 1. Purpose

- The architecture chapters and the change pages describe what is in force.
- When the design changes, they are rewritten in place, and the reasons for the old
  choice are lost.
- Without those reasons, a settled question gets asked again.

## 2. Scope

- Only a major decision gets a record:
    - it was studied and challenged enough that it no longer changes;
    - it changes only when another major decision replaces it.
- Every other choice is described in its chapter or page, with no record.

## 3. Lists

- Each category keeps its own list:
    - `docs/architecture/decisions/`: how the system is built;
    - `docs/changes/decisions/`: what v2 changes from v1 for its users.
- Each list is numbered on its own, from 001.

## 4. Format

- File name: `NNN-short-title.md`, numbered in order, started from the template below.
- Status values:
    - Proposed: under discussion;
    - Accepted: in force;
    - Superseded by NNN: replaced by a later record;
    - Rejected: discussed and turned down.
- A record is not edited after it is accepted.
- A changed decision gets a new record, and the old one is marked "Superseded by NNN".
- The chapter or page describes the decision in force, and links to the record for the
  reasons.

## 5. Template

````markdown
# Short title

- Status: Proposed
- Recorded: YYYY-MM-DD

## 1. Context

- The problem, and the facts that limit the choice.

## 2. Decision

- What was decided, stated first, in one bullet.

## 3. Alternatives

- Each option that was turned down:
    - why it was turned down.

## 4. Consequences

- What becomes easier or harder because of the decision.
- Details: [Chapter](../...), section N.
````
