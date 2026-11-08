# Hangfire for jobs

- Status: Accepted
- Recorded: 2026-11-08

## 1. Context

- The Origin runs recurring jobs, such as the expiry of sends and garbage collection.
- It also runs one-off work, such as malware scans, thumbnails and notification emails.
- In cluster mode, several nodes share that work.

## 2. Decision

- Hangfire, with PostgreSQL storage, is the only scheduling and queue mechanism.

## 3. Alternatives

- No other option is recorded.

## 4. Consequences

- Nodes coordinate through PostgreSQL; no message broker is needed.
- Jobs receive identifiers, not objects, and are retried with backoff.
- Details: [Platform topology](../architecture/platform/topology.md), section 2.
