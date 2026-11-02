# BigFatFile Architecture: Transfer

How bytes move between Apps, the Origin and storage: the file-level endpoints, the
chunk-level endpoints, the tenant-wide scope used by mirroring Apps such as the Replica,
sends, and the transport layer. All of it is one API (`Architecture.API.md`); there
are no private endpoints.

## 1. File-level transfer

- What Apps use regularly. File level only: no chunks or hashes appear in it.
- The upload ID is the node ID (`Architecture.DataModel.md`, section 2): the client
  gives the node a UUIDv7 when it creates it, and every upload endpoint is under
  `/nodes/{id}`.
- Upload: one sequential stream, resumable by offset:
    1. `POST /nodes` with `id`, name, parent and size (`size` optional); the file is not
       in the tree yet.
    2. `PUT /nodes/{id}/content` streams the bytes. The Origin runs the chunker from
       Contracts on the bytes as they arrive and writes chunks straight to the
       warehouse; there is no intermediate object and no background job.
    3. The upload completes when the received bytes reach `size`, or at the end of the
       body when no size was given: the Origin inserts the node, commits the manifest and
       appends the change-log row in one transaction. On a node already in the tree the
       same `PUT` starts a new version; `If-Match` is required.
- Resume:
    - the Origin records the byte offset after the last completed chunk, and writes the
      chunker's carry-over (at most one maximum chunk, 8 MiB) to
      `uploads/<node_id>.partial` in the warehouse, so the offset is exact;
    - `GET /nodes/{id}/upload` returns that offset; the client continues from there with
      `Content-Range`;
    - resume needs `size`; a stream of unknown length is one uninterrupted request.
- What this path gives and does not give:
    - resume and one storage model, like every other file;
    - no parallel parts, no delta against an earlier version, no skipping of chunks the
      tenant already has; those are chunk-level features (section 2);
    - the bytes pass through the Origin, at about 1 GB/s per core for chunking and
      hashing; inbound traffic, not outbound.
- Download: `GET /nodes/{id}/content` with range support; the Origin maps the range to
  chunks and streams only what is requested. Previews and playback (PDF, video) always
  use this.
- The Origin (or the Replica on the LAN) serves share links to drive files the same way;
  a link to several files is served as one streamed zip.
- The SDKs choose the tier: a seekable input of known size goes through the Transfer
  Protocol; a stream of unknown length goes through this path. An App never picks.

## 2. Chunk-level transfer

- The chunk-level endpoints, each with an "Advanced" badge in the reference; consumed
  through the SDKs; documented as "use the SDK unless you are writing one". This is
  where delta, deduplication and client-side assembly live.
- A chunked upload is shaped like S3 multipart, keyed by the node ID:
    1. `POST /nodes` as for a simple upload (skipped for a new version of an existing
       node), then `POST /nodes/{id}/upload` with the manifest; the Origin returns the
       chunks the tenant does not have and, on S3, a presigned URL for each. On a node
       already in the tree `If-Match` is required.
    2. The client uploads the missing chunks in parallel, one request each, to the Origin
       (`PUT /nodes/{id}/upload/chunks/{hash}`) or directly to storage; the Origin
       verifies each chunk's hash on receipt.
    3. `POST /nodes/{id}/upload/complete` inserts the node (new file) or bumps its version
       (new version), commits the manifest and appends the change-log row, with
       `If-Match` on the node's version.
- Resume is automatic:
    - a restarted client calls `GET /nodes/{id}/upload`, or repeats step 1 with the same
      manifest, and is told what is still missing;
    - an upload with no activity for 24 hours expires; a pending new file goes with it.
- Our own clients (desktop, CLI, Replica) assemble downloads themselves:
    - `GET /nodes/{id}/manifest` returns the manifest and, on S3, a presigned
      URL per chunk with a short expiry;
    - the client fetches the missing chunks in parallel (from the Replica, the Origin or
      storage), verifies each hash, and writes them at their offsets;
    - the Origin moves no bytes on S3;
    - errors are caught per chunk so resume is also per chunk.
- Chunks by hash, part of this protocol:
    - `GET /nodes/chunks/{hash}` and `POST /nodes/chunks/exists`;
    - a hash alone grants nothing: the caller presents a grant;
    - fetching the manifest of a node that the caller may read returns a grant for each
      chunk, valid for a few minutes; on S3 the grant is the presigned URL itself;
    - an App holding the `tenant.mirror` scope (section 4) presents that instead; it
      stands for a grant over every chunk of the tenant;
    - chunks never enter the Origin by hash; they arrive only through a chunked upload
      or the file-level upload, so nothing orphaned arrives and garbage collection's
      pinning holds.
- Downloads in the browser:
    - `@bigfatfile/client` assembles the file in the browser when a streaming download is
      possible (service worker, or the File System Access API), otherwise falls back to
      the file-level download (section 1).
- The web uploader splits the file into chunks with FastCDC and hashes them with BLAKE3,
  in WebAssembly, in a web worker;
    - the desktop client does the same natively.

## 3. Sends (transfers)

- A send (a transfer to recipients outside the system) is stored as whole-file objects,
  not chunks:
    - a send is immutable, has no versions and needs no delta;
    - most of its traffic is downloads by external recipients on any browser or phone,
      and each of those must be a plain download.
- Upload:
    - presigned multipart upload to storage on S3 warehouses;
    - direct `PUT` to the Origin on `local` warehouses;
    - resume via the multipart mechanism.
- Download:
    - presigned URL from storage on S3 warehouses;
    - on `local` warehouses the Origin streams the object.
    - multi-file sends (transfers) are downloaded as one streamed zip.
- Expiry deletes the objects.

## 4. Tenant-wide Apps (`tenant.mirror`)

- Everything here is in the one public specification, under the `mirror` tag. What
  separates it from the rest is a scope, not privacy.
- The `tenant.mirror` scope:
    - granted deliberately by a tenant administrator to an App that mirrors or backs up
      the whole tenant; the Replica is the first-party App that holds it;
    - lets the App fetch any chunk of the tenant by hash (section 2) and read the whole
      change feed without filtering.
- Bootstrap and catch-up:
    - `GET /snapshot`: the tenant tree with every node's current version and manifest
      ID, in the same format as the daily warehouse snapshot;
    - `GET /nodes/{id}/versions` for retained history;
    - afterwards the App follows the change feed from the snapshot's sequence number.
- Delegated identity: token exchange (RFC 8693) on the auth endpoints. An App presents
  its own token and an assertion "on behalf of user X" and receives a token carrying
  both, so a write operation that was accepted offline can be replayed hours later; the
  audit log records the user as actor and the App as agent. Available to any App whose
  scopes allow it.
- The Replica additionally registers itself and reports status through the `replicas`
  resource of the API: pairing with a one-time code, periodic status (lag,
  completeness, last verification), and the list that clients read to find a Replica's
  LAN address and public key.

## 5. Transport

- Protocols:
    - HTTP/2 to our own server from every client;
    - HTTP/3 where the client OS and the reverse proxy support it, but not required;
    - direct-to-storage transfers assume HTTP/1.1:
        - AWS S3, Backblaze B2, Garage and MinIO serve presigned URLs over HTTP/1.1 only;
        - Cloudflare R2 also serves HTTP/2 and HTTP/3.
- Throughput:
    - high throughput relies on the number of parallel TCP connections,
      not the protocol or the chunk size;
    - one TCP connection is limited by its congestion control:
        - note that a lost packet makes the default algorithm (CUBIC) halve its sending rate and
          recover slowly over many round trips;
    - HTTP/2 streams share one TCP connection, so adding streams never exceeds what one connection can do;
    - several connections each recover on their own and together fill the uplink to the maximum.
- Transfer engine rules:
    - 4 to 8 chunks in flight, spread over several TCP connections:
        - an HTTP/1.1 keep-alive pool, or...
        - `EnableMultipleHttp2Connections` in .NET;
    - the in-flight count adapts to measured throughput, and backs off when more
      connections only add loss;
    - each chunk has its own timeout and is retried with backoff;
    - a stalled chunk costs only that one chunk, not the whole file;
    - progress is reported continuously, so a slow transfer is never mistaken for a hang.
- HTTP/2 flow-control windows:
    - set explicitly on the receiving side, because defaults are tuned for web pages:
        - Kestrel (uploads):
            - `InitialStreamWindowSize` 8 MB;
            - `InitialConnectionWindowSize` 32 MB;
        - .NET `HttpClient` (downloads to desktop, CLI and Replica):
            - `InitialHttp2StreamWindowSize` 16 MB;
        - reverse proxies that terminate HTTP/2 own the windows for that hop:
            - e.g. for NGINX set `http2_body_preread_size`
- TCP congestion control:
    - on our hosts (Origins and Replicas):
        - we use BBR with the `fq` queueing discipline
          (`net.core.default_qdisc=fq`, `net.ipv4.tcp_congestion_control=bbr`);
        - BBR paces packets evenly instead of sending in bursts, so a shallow buffer on the
          path does not drop them, so one connection can reach the maximum link speed;
        - BBR does not slow down on loss, so it resends more and can take bandwidth from
          CUBIC connections sharing the same bottleneck;
            - acceptable on a server sending to many clients.
    - on Client hosts:
        - Windows and macOS do not let an application choose the congestion control;
        - so parallel connections are the only way to fill an uplink to the maximum;
        - on Linux, the client documents BBR as an optional setting but never sets it itself.
- Measured baseline: office uplink to a Webdock VPS, 54 ms round trip, about 48 Mbit/s,
  `bench/upload-chunks`:

| Sender                    | One connection                                     | Four or more connections |
| ------------------------- | -------------------------------------------------- | ------------------------ |
| CUBIC, HTTP/1.1 or HTTP/2 | about 3 MB/s, falling to 0.6 MB/s under heavy loss | about 5.5 MB/s           |
| BBR with `fq`             | about 5.9 MB/s                                     | link speed               |

- Chunk size (4, 16 or 64 MB) made no measurable difference in any row.
