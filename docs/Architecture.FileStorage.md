# BigFatFile Architecture: Content Storage

How file content is stored: chunking and manifests, warehouses, garbage collection,
encryption.

## 1. Chunking and manifests

- Plain sends (transfers) are stored as whole objects (`Architecture.FileTransfer.md`).
- Drive files are stored in chunks:
    - a chunk is a variable-size piece of the file;
    - chunks are isolated (cut) by content-defined chunking (FastCDC);
- Content-defined chunking:
    - the chunker places each boundary where the bytes match a pattern, so boundaries
      follow the content, not the offset;
    - chunk sizes: 1 MiB minimum, 4 MiB average, 8 MiB maximum;
    - identified by the BLAKE3 hash of its bytes;
    - stored once per tenant within a warehouse, possibly referenced by many files:
        - a chunk is never reused across tenants;
        - cross-tenant sharing would let anyone test whether a given file exists somewhere
          in the system by asking "do you already have this chunk?";
        - it would also make "your data is deleted" untrue while another tenant still
          references a chunk.
- A file version is a manifest structure of:
    - the ordered list of chunk hashes and sizes;
    - the total size;
    - the hash of the whole file.

```json
{
    "chunks": [
        { "hash": "blake3:…", "size": 4194304 },
        { "hash": "blake3:…", "size": 2097152 }
    ],
    "size": 104857600,
    "hash": "blake3:…"
}
```

- Why content-defined and not fixed-size chunks:
    - an overwrite in place (databases, mail stores, VM images) changes only the chunks
      that hold the changed bytes, with either method;
    - an insert or delete shifts every later byte; with fixed offsets every later chunk
      then gets a new hash, so the rest of the file is uploaded again;
    - content-defined boundaries re-align within one chunk after the change, so only
      the chunks around it are new;
    - most design and media tools (Photoshop, TIFF, CAD, Blender, audio and video
      projects) and zip containers such as Office files rewrite the file with sections
      at new offsets, so they need content-defined chunking to get a delta.
- Chunker parameters (minimum, average and maximum size, the boundary mask) are fixed in
  Contracts. The C# and WebAssembly chunkers must cut identically, which the shared test
  vectors check: a different cut means no deduplication between web and desktop
  uploads of the same file.
- Performance:
    - chunking runs where files are produced (desktop, CLI, Replica, browser) through
      the SDKs, and on the Origin, inline, for uploads through the file-level
      endpoints (`Architecture.FileTransfer.md`); it and hashing are one streaming pass over the
      file;
    - about 0.7 to 1 GB/s per core natively and 150 to 300 MB/s in WebAssembly,
      far above any uplink;
    - memory: one chunk per upload in flight must be buffered for hashing, so the 8 MiB
      maximum keeps 8 uploads in flight at 64 MB;
    - mapping a byte offset to a chunk is a binary search over the manifest's
      cumulative sizes;
    - a small read inside a dehydrated file fetches the whole chunk containing it, up to
      8 MiB; the first read may fetch a byte range of the chunk object and verify the
      chunk once the rest arrives.
- The manifest has two copies:
    - database: the working copy; used for range-to-chunk mapping, the missing-chunks
      check, garbage collection, version history and the `If-Match` check;
    - warehouse (`manifests/<tenant>/<id>`): the recovery copy, written once at commit
      and never read in normal operation; carries tenant, node ID, version, size,
      timestamps and the path at commit time; read only by `bff export`, disaster
      recovery and integrity audits.
- A daily job writes a tree snapshot per tenant into the warehouse
  (`snapshots/<tenant>/<date>.json`), so manifests plus the latest snapshot rebuild
  names, folders and contents without the database.
- Commit order: chunks, then the warehouse manifest, then the database transaction. The
  database never references what the warehouse lacks. The database is authoritative
  when the two disagree, except in recovery mode.
- What chunking gives: upload of only the changed parts, instant upload of a file the
  tenant already has, resumable and parallel transfers, integrity checks per chunk,
  cheap versioning (a version costs only its new chunks), and a Replica that moves only
  changed chunks.

## 2. Warehouses

- A warehouse is a named storage target: an ID, a type and the settings for that type.
- Types in the open core:
    - `local`: a directory;
    - `s3`: endpoint, bucket, region, credentials, whether presigned URLs may be issued;
      works with Cloudflare R2, Backblaze B2, Wasabi, Garage, RustFS, MinIO.
- Two sources, merged by `IWarehouseRegistry`:
    - server configuration, owned by the operator (open core):
        - a default warehouse is mandatory (`BFF_DEFAULT_WAREHOUSE` names it); more are
          optional;
        - definitions live in a mounted file (`/config/warehouses.json`); credentials are
          referenced from environment variables or Docker secrets, never written in the
          file;
    - the database, defined by tenant administrators in the web UI, credentials
      encrypted at rest (licensed module `storage.tenant-warehouses`); this is how a
      tenant brings its own bucket.
- Every tenant has a `warehouse_id`; new tenants get the default unless an administrator
  assigns another.
- Chunk identity is `(warehouse_id, tenant_id, hash)`: deduplication, the missing-chunks
  check and garbage collection run within one tenant of one warehouse.
- Moving a tenant between warehouses is a job: copy the referenced chunks, switch the
  tenant, let garbage collection clean the old location.
- Startup checks:
    - probe every configured warehouse (write and read a test object);
    - verify every warehouse referenced by a tenant exists;
    - either failure stops the Origin.
- Cluster mode requires every warehouse to be shared storage (S3 or a shared
  filesystem); a `local` warehouse on one node's disk fails the check.
- `local` implementation:
    - `chunks/<tenant>/ab/cd/<hash>` with two levels of fan-out; `objects/<tenant>/<id>`
      for sends; `manifests/` and `snapshots/` as in section 1;
    - write to a temporary file in the same directory, fsync, rename into place;
    - any local filesystem works; network filesystems work too (nothing locks).
- `s3` implementation:
    - the AWS SDK with a configurable endpoint;
    - presigned URLs let browsers and clients move bytes directly to and from storage.

## 3. Garbage collection

- A chunk is garbage when no retained version of the tenant references it.
- Mark and sweep per tenant: list every referenced hash, list the tenant's chunks,
  delete what is stored but not referenced.
- A pruned version's warehouse manifest is deleted the same way; an expired send's
  object is deleted when the send expires.
- Two safeguards for chunks in flight:
    - an upload session pins its chunks until it completes or expires;
    - the sweep never deletes a chunk written less than 24 hours ago.
- Runs as a recurring Hangfire job.

## 4. Encryption

- Client-side (end-to-end) encryption is not part of v2.
- It would remove deduplication within a tenant, server-side malware scanning, and the
  Replica's ability to serve plain files on the LAN.
- It stays a research item; the chunk model does not prevent adding it later for
  specific drives.
