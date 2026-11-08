# Recording decisions

- Status: Accepted
- Recorded: 2026-11-08

## 1. Context

- The architecture chapters describe the design that is in force.
- The reasons for a design, and the options that were turned down, are lost when a
  chapter is rewritten in place.
- Without those reasons, a settled question gets asked again.

## 2. Decision

- Each big decision gets its own record in `docs/decisions/`.
- File name: `NNN-short-title.md`, numbered in order (start from `000-template.md`).
- Status values:
    - Proposed: under discussion;
    - Accepted: in force;
    - Superseded by NNN: replaced by a later record;
    - Rejected: discussed and turned down.

## 3. Alternatives

- Reasons kept inside the chapters:
    - they make the chapters longer;
    - they get lost when the design changes and the chapter is rewritten.
- Reasons kept only in the Roadmap:
    - the Roadmap is about the order of the work, not about the design.

## 4. Consequences

- Chapters stay current-state only, and link to a record for the reasons.
- A changed decision gets a new record; the old one is marked as superseded.
