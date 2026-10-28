# BigFatFile (BFF) - Architecture

A brief overview of the technology stack for the BigFatFile platform, including the specific libraries and tools for implementation.

### Frontend (Web UI)

- [x] SvelteKit (Frontend framework)
- [x] Bootstrap v5.3 (UI framework)
- [x] LingUI (I18n/L10n)
- [ ] Auth.js (Authentication)
- [ ] Zod (Validation)
- [ ] TanStack-Query (SWR fetching)
- [ ] SignalR (WebSockets)

### Back-End (API)

- [x] .NET 9 (Platform)
- [x] TypeGen (TypeScript type generator)
- [x] Dapper (ORM)
- [x] PgSQL Row-Level Security (Multi-tenancy)
- [x] MS Hangfire (CRON distributed scheduling)
- [ ] JWT (Authentication)
- [ ] SignalR (WebSockets)

### Shared Domain & Tooling

- [x] Dapper ORM
- [x] TypeGen (generate TypeScript from C#)

### Database

- [x] PostgreSQL/TimescaleDB
- [x] Dapper ORM/Npgsql
- [x] Badgie.Migrator
- [x] TIMESCALEDB extension
- [x] LTREE extension
- [ ] LISTEN/NOTIFY & TRIGGER for RT-updates

### Cross-Platform Agent

- [ ] .NET 9 (Platform)
- [ ] Avalonia UI
- [ ] Duende.IdentityModel.OidcClient
- [ ] Dokany.NET (Windows)
- [ ] P/Invoke for libfuse (macOS/Linux)
- [ ] LightningDB (wrapper for LMDB)
- [ ] SignalR

### Infrastructure

- [ ] Cloudflare R2
- [ ] Cloudflare Workers (frontend deployment)
