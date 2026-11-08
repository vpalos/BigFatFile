# Architecture decision records

An architecture decision record (ADR) states one big decision, why it was taken, and
which options were turned down.

## 1. Rules

- One decision per record.
- A record is not edited after it is accepted:
    - a new decision gets a new record;
    - the old record's status becomes "Superseded by" and links to the new one.
- The architecture chapters describe the design in force and link to the record for the
  reasons.
- A record names the chapter section that holds the details, and does not repeat them.

## 2. Files

- One file per record: `NNNN-short-title.md`, numbered in order.
- The title is a short plain noun phrase, such as "Row-level security":
    - the number is in the file name, not in the title.
- A number is never reused, even when a record is deleted.
- A new record starts as a copy of `0000-template.md`.
- The site lists the records in the sidebar on its own.

## 3. Statuses

- Proposed: under discussion.
- Accepted: in force.
- Superseded by NNNN: replaced by a later record.
- Rejected: discussed and turned down; kept so that the question is not asked again.
