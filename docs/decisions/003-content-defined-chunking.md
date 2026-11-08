# Content-defined chunking

- Status: Accepted
- Recorded: 2026-11-08

## 1. Context

- Edited files should upload only the parts that changed.
- Many design and media tools, and zip containers such as Office files, rewrite the
  whole file with its sections at new offsets.
- Deduplication across tenants would let anyone test whether a file exists somewhere in
  the system.

## 2. Decision

- Drive files are split into chunks by a content-defined chunker:
    - FastCDC, the `v2020` chunker of the Rust crate `fastcdc` 5.0.0;
    - chunks between 1 MiB and 8 MiB with the default settings;
    - each chunk is identified by the BLAKE3 hash of its bytes.
- A chunk is stored once per tenant within a warehouse, and is never shared between
  tenants.
- Sends are stored as whole objects.

## 3. Alternatives

- Fixed-size chunks:
    - inserting or deleting one byte gives every later chunk a new hash, so the rest of
      the file is uploaded again.
- Whole objects for every file:
    - no delta uploads, no per-chunk resume, and every version costs a full copy.
- Deduplication across tenants:
    - "do you already have this chunk?" would reveal other tenants' files;
    - "your data is deleted" would be untrue while another tenant still referenced a
      chunk.

## 4. Consequences

- The C# and WebAssembly builds must split a file identically; shared test vectors check
  this.
- Garbage collection must count references per tenant.
- Details: [File storage](../architecture/files/storage.md), section 1.
