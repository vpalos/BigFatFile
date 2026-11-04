# BigFatFile Architecture: Content Storage

How file content is stored: chunking and manifests, warehouses, garbage collection,
encryption.

## 1. Chunking and manifests

### Two ways to store a file

- A file's content is stored in one of two forms:
    - as chunks: the file is cut into pieces, and each piece is stored once;
    - as one whole object: the file is stored as it is, in one piece.
- Drive files are stored as chunks by default.
- Whole objects are used for:
    - sends, which never change and are downloaded whole;
    - uploads for which chunking on the Origin is switched off;
    - files that existed in storage before they were chunked.
- A manifest describes which form a file version has, and where its bytes are. Every
  file version has one manifest.

### What chunking gives

- Only the changed parts of an edited file are uploaded.
- A file the tenant already has is uploaded instantly, because its chunks are there.
- Transfers can run in parallel and resume per chunk.
- Every chunk is checked against its hash, so corruption is found per chunk.
- A version costs only its new chunks.
- A Replica moves only the chunks that changed.

### Chunks

- A chunk is a variable-size piece of a file, between 1 MiB and 8 MiB with the default
  settings.
- A chunk is identified by the BLAKE3 hash of its bytes.
- A chunk is stored once per tenant within a warehouse, however many files reference it.
- A chunk is never shared between tenants, for two reasons:
    - sharing would let anyone test whether a file exists somewhere in the system, by
      asking "do you already have this chunk?";
    - "your data is deleted" would be untrue while another tenant still referenced a
      chunk.

### Why the boundaries follow the content

- The chunker is content-defined (FastCDC): it places each boundary where the bytes
  match a pattern, so a boundary moves with the content, not with the offset.
- With fixed-size chunks, inserting or deleting one byte shifts every later byte, every
  later chunk gets a new hash, and the rest of the file is uploaded again.
- With content-defined chunks, the boundaries line up again within one chunk after the
  change, so only the chunks around the change are new.
- An overwrite in place (databases, mail stores, VM images) changes only the chunks that
  hold the changed bytes with either method.
- Most design and media tools rewrite the whole file with its sections at new offsets,
  and so do zip containers such as Office files. These need content-defined chunking to
  get a delta. Examples: Photoshop, TIFF, CAD, Blender, audio and video projects.

### The chunker definition

- The chunker is defined once, in Contracts, and the C# and WebAssembly builds must cut
  a file identically. A different cut means no deduplication between a web upload and a
  desktop upload of the same file.
- The definition is the `v2020` chunker of the Rust crate `fastcdc` 5.0.0:
    - its GEAR table, no seed;
    - normalisation level 1, which uses the 8 MiB mask below the average size and the
      2 MiB mask above it.
- The shared test vectors are generated with that crate, so the WebAssembly build of
  the same crate matches by construction.
- On 64 MiB of random bytes the chunks average about 6 MiB, and 3 of the 11 stop at the
  8 MiB maximum. A chunk cut at the maximum size ends at a fixed offset, not at a
  content boundary.

### Manifests

- A manifest holds:
    - the chunking that cut the file: the algorithm and its parameters, or `none`;
    - for a chunked file, the ordered list of chunk hashes and sizes;
    - for a whole object, the ID of the object;
    - the total size;
    - the hash of the whole file, where known.

```json
{
    "chunking": { "algorithm": "fastcdc", "gear": 1, "min": 1048576, "avg": 4194304, "max": 8388608 },
    "chunks": [
        { "hash": "blake3:…", "size": 4194304 },
        { "hash": "blake3:…", "size": 2097152 }
    ],
    "size": 104857600,
    "hash": "blake3:…"
}
```

- Every manifest follows these rules, whoever produced it:
    - one manifest, one chunking;
    - each chunk has between 1 byte and the maximum of its chunking;
    - only the last chunk may be smaller than the minimum of its chunking;
    - the chunk sizes add up to the file size;
    - an empty file has no chunks;
    - with `none`, there are no chunks and `object` names the whole file.
- How a manifest relates to the tree and to storage is in `Architecture.DataModel.md`,
  section 3.

### Chunking parameters

- The parameters are recorded in each manifest, so files cut with different parameters
  live side by side.
- `gear` names the GEAR table. A different table cuts differently, so it is one of the
  parameters.
- A new version of a file is cut with the parameters of the version it replaces. This
  keeps delta and deduplication working after the defaults change.
- A file whose parameters are no longer supported is cut with the current defaults
  once. From the next version on it has its delta back.
- The server accepts only parameter sets it knows, listed in `GET /meta`. A client
  cannot invent one.

### Whole objects

- A manifest with `"chunking": { "algorithm": "none" }` describes a file stored as one
  whole object. It has `object`, the object's ID, instead of `chunks`. `hash` is
  optional, and is kept when the client computed it while uploading.
- A whole object is a second kind of thing in a warehouse, next to chunks:
    - its identity is `(warehouse, tenant, object ID)`, the ID a UUIDv7;
    - chunk identity stays `(warehouse, tenant, hash)`; the two never mix;
    - each object has one row in `objects`: ID, size, storage key, and the hash when
      known (`Architecture.DataModel.md`, section 3);
    - the key follows the warehouse layout (section 2) by default, but a row may carry
      any key. This is what lets an object that already exists in storage be adopted
      without being read or copied;
    - an object is never deduplicated;
    - garbage collection marks objects by ID from the manifests and sweeps them by their
      recorded keys, in the same pass as chunks;
    - an object is always written and read as a stream, never held in memory; on S3 it
      is a multipart upload.
- What works for a whole object: resume (through multipart), range reads (a range of one
  object is a range read on storage), versions, and mirroring by the Replica.
- What does not: delta and deduplication. A new version of a `none` file is cut with the
  tenant's current default chunking, which may itself be `none`.
- Integrity without a hash: on S3 every part is sent with `Content-MD5` and storage
  verifies it. A client that has the bytes anyway computes BLAKE3 while sending, and
  the manifest keeps it.
- Where whole objects are used:
    - sends (`Architecture.FileTransfer.md`, section 3);
    - file-level uploads when inline chunking is off, by a tenant setting or by a choice
      per upload, so the cost of chunking on the Origin can be turned off;
    - files that existed before they were chunked, adopted by key. Whether and when such
      a file is chunked is decided later, per file, on edit or on access.

### Where a manifest lives

- The manifest has two copies:
    - the database copy is the working copy: range-to-chunk mapping, the missing-chunks
      check, garbage collection, version history and the `If-Match` check all read it;
    - the warehouse copy (`manifests/<tenant>/<id>`) is the recovery copy: written once
      at commit, never read in normal operation; it also carries the tenant, node ID,
      version, size, timestamps and the path at commit time; read only by `bff export`,
      disaster recovery and integrity audits.
- A daily job writes a tree snapshot per tenant into the warehouse
  (`snapshots/<tenant>/<date>.json`). Names, folders and contents can be rebuilt from
  the manifests and the latest snapshot, without the database.
- Commit order: chunks first, then the warehouse manifest, then the database
  transaction. The database never references what the warehouse lacks. When the two
  disagree, the database is authoritative, except in recovery mode.

### Performance

- Chunking runs where files are produced (desktop, CLI, Replica, browser) through the
  SDKs, and on the Origin, inline, for uploads through the file-level endpoints
  (`Architecture.FileTransfer.md`).
- Chunking and hashing happen in one streaming pass over the file.
- Speed: about 0.7 to 1 GB/s per core natively, and 150 to 300 MB/s in WebAssembly,
  far above any uplink.
- Memory: one chunk per upload in flight is buffered for hashing, so the 8 MiB maximum
  keeps 8 uploads in flight at 64 MB.
- Mapping a byte offset to a chunk is a binary search over the manifest's cumulative
  sizes.
- A small read inside a dehydrated file fetches the whole chunk that contains it, up to
  8 MiB. The first read may fetch a byte range of the chunk and verify the chunk once
  the rest arrives.

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
    - the database: warehouses that tenant administrators define in the web UI, with
      credentials encrypted at rest (licensed module `storage.tenant-warehouses`); this
      is how a tenant brings its own bucket.
- Every tenant has a `warehouse_id`; new tenants get the default unless an administrator
  assigns another.
- Chunk identity is `(warehouse_id, tenant_id, hash)`: deduplication, the missing-chunks
  check and garbage collection run within one tenant of one warehouse.
- Moving a tenant between warehouses is a job: copy the referenced chunks, switch the
  tenant, let garbage collection clean the old location.
- Startup checks:
    - probe every configured warehouse (write and read a test object);
    - check that each warehouse that a tenant references exists;
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
    - an upload in progress pins its chunks until it completes or expires
      (`Architecture.DataModel.md`, section 2);
    - the sweep never deletes a chunk written less than 24 hours ago.
- Runs as a recurring Hangfire job.

## 4. Encryption

- Client-side (end-to-end) encryption is not part of v2.
- It would remove deduplication within a tenant, server-side malware scanning, and the
  Replica's ability to serve plain files on the LAN.
- It stays a research item; the chunk model does not prevent adding it later for
  specific drives.
