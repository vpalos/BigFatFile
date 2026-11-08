# Desktop file materialisation

- Status: Accepted
- Recorded: 2026-11-08

## 1. Context

- The desktop client must show a very large remote drive (for example 20 TB) as a local
  folder, right after install and sign-in.
- Files must open at the speed of a local disk.
- Applications that write in place must work: databases, Outlook data files, design
  tools, and the Office save sequence.

## 2. Decision

- The desktop drive uses materialisation:
    - the folder is a real folder on the native filesystem;
    - entries are created lazily, and are hollow until a file is opened;
    - file content is kept in a bounded local cache, least recently used first.
- Each platform uses its own native API:
    - Windows: Cloud Files API;
    - macOS: File Provider;
    - Linux has no such API, so it uses FUSE with a write-back cache that follows the
      same model.

## 3. Alternatives

- Projection (FUSE on every platform, WinFsp or Dokany on Windows, ProjFS):
    - our process would have to answer every filesystem operation with full semantics;
    - every operation would pay a round trip through our process;
    - WinFsp and Dokany need a kernel driver;
    - ProjFS has no sync-status integration in Explorer;
    - if our process stops, a projected drive hangs or disappears.
- WebDAV:
    - not the experience the product needs.
- A full two-way mirror of the drive on each workstation:
    - a 20 TB drive does not fit on a workstation;
    - the full mirror is the job of the Replica.

## 4. Consequences

- A hydrated file is an ordinary local file, and keeps working if our process stops.
- One sync engine runs behind three platform adapters.
- Details: [File materialisation](../architecture/files/materialisation.md), sections 2,
  5 and 6.
