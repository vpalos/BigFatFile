# BigFatFile Architecture: Observability

How the Origin, the Replica and the clients report traces, metrics and logs, what BFF
measures about itself, and where the data goes.

## 1. Briefing

- OpenTelemetry is the one mechanism for all three signals:
    - traces: what one request or job did, step by step, with timings;
    - metrics: counts and sizes over time;
    - logs: the log lines, carrying the trace ID of the request that caused them.
- The app exports over OTLP (the OpenTelemetry wire protocol) and never knows which
  backend receives it.
    - Export is off unless `OTEL_EXPORTER_OTLP_ENDPOINT` is set. Configuration uses the
      standard `OTEL_*` environment variables, so operators point it wherever they already
      collect telemetry.
- A Prometheus endpoint at `/metrics` is available as well, because many self-hosters
  already run Prometheus and Grafana and want nothing more.
- The `/health` endpoint stays as it is; observability does not replace it.

## 2. Origin

- Built-in instrumentation, no BFF code involved:
    - ASP.NET Core: one span per request with route, status and duration; `/health`
      excluded;
    - Npgsql: one child span per SQL statement;
        - only inside a recorded request or job span: queue polls, health probes and
          the pool opening connections are not traced, because they would bury the
          useful traces;
    - `HttpClient` and the AWS SDK: outgoing calls, including every S3 request;
    - Hangfire: one span per job run;
    - SignalR: connection and message counters;
    - .NET runtime: GC, thread pool, exceptions;
    - logging: `ILogger` lines exported with trace and span IDs.
- BFF's own signals come from one `ActivitySource("BigFatFile")` and one
  `Meter("BigFatFile")`, both defined in `Abstractions` so licensed modules report
  into the same stream.
- Spans:
    - upload session: one parent span from create to complete, with chunk count, bytes
      received, bytes deduplicated and the warehouse as attributes; no span per chunk,
      which would be too much volume; a failed chunk is an event on the session span;
    - download and share-link stream: bytes served and whether they came from a `local`
      or an `s3` warehouse;
    - manifest commit and change-log append;
    - garbage collection run per warehouse: chunks marked, swept and deleted bytes;
    - module loading and licence checks.
- Metrics:
    - bytes uploaded and downloaded, by warehouse and by route (through the Origin,
      direct to storage);
    - chunk deduplication hit ratio;
    - active upload sessions; session failures and timeouts;
    - chunk hash verification failures (expected to be zero; any value is an alert);
    - garbage collection: bytes freed, duration, last completed time;
    - real-time hub connections per node;
    - Hangfire queue depth and failed jobs (from its own instrumentation).
- Cardinality rule:
    - tenant ID is an attribute on traces and logs, never a metric label on the cloud;
    - metrics are labelled by warehouse, route and plan, which stay few;
    - a dedicated instance with one tenant may label by tenant.

## 3. Replica

- Same SDK and the same `BigFatFile` source and meter; service name `bff-replica`.
- Additional metrics: lag behind the Origin (sequence numbers), outbox depth, chunks
  mirrored and verified, reconcile duration and conflicts.
- Exports to the office's collector when one is configured; the existing status report
  to the Origin remains the primary health signal and does not depend on telemetry.
- Trace context arrives from clients in the `traceparent` header and is forwarded to
  the Origin, so one trace can follow an upload from the desktop through the Replica to
  the Origin.

## 4. Clients

- Desktop:
    - nothing is exported from a user's machine by default; the published pledges rule
      out silent telemetry;
    - the engine keeps traces and metrics in an in-process ring buffer; the settings
      window shows them and `bff diag` writes them to a file the user can send for
      support;
    - remote crash or performance reporting, if ever offered, is an explicit opt-in
      switch described in plain words.
- CLI: a `--trace` flag exports the trace of one run; otherwise off.
- Web UI: the browser SDK is deferred; it adds 50 to 100 KB and needs a CORS header for
  `traceparent`. Error reporting from the browser is a separate, smaller decision.

## 5. Where the data goes

- Development: the .NET Aspire dashboard, one container
  (`mcr.microsoft.com/dotnet/aspire-dashboard`), accepts OTLP and shows traces, metrics
  and logs with no configuration. Part of the development compose override.
- Self-hosters: nothing is bundled. The OTLP variables and the Prometheus endpoint are
  documented, and an `observability` compose profile starts an OpenTelemetry Collector
  plus one all-in-one backend for those who want it:
    - OpenObserve: single binary, all three signals, light;
    - SigNoz: ClickHouse-based, heavier, richer UI;
    - the full Grafana stack (Tempo, Loki, Mimir, Grafana) is four containers and is not
      the recommended default.
- Cloud: one backend on a separate small VPS (OpenObserve or SigNoz), or a hosted free
  tier while volume is small. Everything goes through a Collector, so sampling and
  retention are set in one place and not in the app.

## 6. Sampling and overhead

- Traces: 100% in development; in production about 10% by head sampling in the app,
  and the Collector keeps every trace that contains an error (tail sampling).
- Metrics and logs are never sampled.
- Overhead: 1 to 3% CPU with sampled traces; memory a few megabytes for buffers.
- Volume: 1 to 2 KB per sampled request trace; a few hundred metric series per node;
  logs as today.

## 7. Alerts

- The short list that means something is wrong:
    - any chunk hash verification failure;
    - upload session failure rate above a threshold;
    - garbage collection not completed within its schedule;
    - Replica lag growing for longer than its reconcile interval;
    - Hangfire failed jobs;
    - `/health` failing.
- Alerts live in the backend, not in the app.
