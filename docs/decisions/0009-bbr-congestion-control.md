# BBR congestion control

- Status: Accepted
- Recorded: 2026-11-08

## 1. Context

- With the default congestion control (CUBIC), a lost packet halves a connection's
  sending rate, and recovery takes many round trips.
- Measured on an office uplink to a Webdock VPS (54 ms round trip, about 48 Mbit/s):
    - one CUBIC connection reached about 3 MB/s;
    - one BBR connection with `fq` reached about 5.9 MB/s;
    - four or more BBR connections reached link speed.

## 2. Decision

- Origin and Replica hosts use BBR with the `fq` queueing discipline.

## 3. Alternatives

- CUBIC with more parallel connections only:
    - four or more CUBIC connections reached about 5.5 MB/s, below link speed.

## 4. Consequences

- BBR does not slow down on loss, so it can take bandwidth from CUBIC connections on the
  same path; this is acceptable on a server that sends to many clients.
- Client hosts still rely on parallel connections, because Windows and macOS do not let
  an application choose the congestion control.
- Details: [File transfer](../architecture/files/transfer.md), section 5.
