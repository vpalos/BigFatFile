# BigFatFile Architecture: Transfer

How bytes move between Apps, the Origin and storage: the file-level endpoints, the
chunk-level endpoints, the tenant-wide scope that mirroring Apps such as the Replica
use, sends, and the transport layer. All of it is one API (`Architecture.API.md`); there
are no private endpoints.

## 1. File-level transfer (`files` tag)

- What Apps use regularly. File level only: no chunks, hashes or sessions appear in it.
- Upload: one sequential stream, resumable by offset:
    1. `POST /files` with name, parent and size; the Origin returns an upload ID.
    2. `PUT /files/{id}/content` streams the bytes. The Origin runs the chunker from
       Contracts on the bytes as they arrive and writes chunks straight to the
       warehouse; there is no intermediate object and no background job.
    3. The Origin attaches the manifest when the last byte arrives; `If-Match` applies as
       for every change.
- Resume:
    - the Origin records the byte offset after the last completed chunk;
    - `GET /files/{id}/upload` returns that offset; the client continues from there;
    - the Origin keeps only the chunker's carry-over buffer per open upload, at most one
      maximum chunk (8 MiB).
- What this path gives and does not give:
    - resume and one storage model, like every other file;
    - no parallel parts, no delta against an earlier version, no skipping of chunks the
      tenant already has; those are chunk-level features (section 2);
    - the bytes pass through the Origin, at about 1 GB/s per core for chunking and
      hashing; inbound traffic, not outbound.
- Download: `GET /files/{id}/content` with range support; the Origin maps the range to
  chunks and streams only what is requested. Previews and playback (PDF, video) always
  use this.
- Share links to drive files are served the same way by the Origin (or the Replica on
  the LAN); multi-file links as a streamed zip.
- The SDKs choose the tier: a seekable input of known size goes through the Transfer
  Protocol; a stream of unknown length goes through this path. An App never picks.

## 2. Chunk-level transfer (`transfer` tag)

- The chunk-level endpoints, marked "advanced" in the reference; consumed through the
  SDKs; documented as "use the SDK unless you are writing one". This is where delta,
  deduplication and client-side assembly live.
- Uploads use an upload session shaped like S3 multipart:
    1. `POST /transfer/sessions` with the manifest; the Origin returns the chunks the tenant does
       not have and, on S3, a presigned URL for each.
    2. The client uploads the missing chunks in parallel, one request each, to the Origin
       (`PUT /transfer/sessions/{id}/chunks/{hash}`) or directly to storage; the Origin verifies
       each chunk's hash on receipt.
    3. `POST /transfer/sessions/{id}/complete` attaches the manifest to the node, with `If-Match` on
       the node's version.
- Resume is automatic:
    - a restarted client repeats step 1 and is told what is still missing;
    - sessions expire after a configurable time.
- Downloads by our own clients (desktop, CLI, Replica) assemble on the client:
    - `GET /nodes/{id}/manifest` returns the manifest and, on S3, a presigned
      URL per chunk with a short expiry;
    - the client fetches the missing chunks in parallel (from the Replica, the Origin or
      storage), verifies each hash, and writes them at their offsets;
    - the Origin moves no bytes on S3;
    - errors are caught per chunk so resume is also per chunk.
- Chunks by hash, part of this protocol:
    - `GET /transfer/chunks/{hash}` and `POST /transfer/chunks/exists`;
    - a hash alone grants nothing: the caller presents a grant, which is what a
      manifest fetch returns for the chunks of a node the caller may read, valid for
      minutes; on S3 the grant is the presigned URL itself;
    - an App holding the `tenant.mirror` scope (section 4) presents that instead; it
      stands for a grant over every chunk of the tenant;
    - chunks never enter the Origin by hash; they arrive only through an upload session
      or the file-level upload, so nothing orphaned arrives and garbage collection's
      pinning holds.
- Downloads in the browser:
    - `@bigfatfile/client` assembles in the browser where a streaming download is
      possible (service worker, or the File System Access API), otherwise falls back to
      the file-level download (section 1).
- The web uploader chunks the file with FastCDC and hashes with BLAKE3 in WebAssembly,
  in a web worker;
    - the desktop client does the same natively.

## 3. Sends (transfers)

- A send (a transfer to recipients outside the system) is stored as whole-file objects,
  not chunks:
    - a send is immutable, has no versions and needs no delta;
    - its traffic is mostly downloads by external recipients on any browser or phone,
      which must be a plain download.
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
  both, so a write accepted offline can be replayed hours later; the audit log records
  the user as actor and the App as agent. Available to any App whose scopes allow it.
- The Replica additionally registers itself and reports status through the `replicas`
  resource of the API: pairing with a one-time code, periodic status (lag,
  completeness, last verification), and the list clients read to find a Replica's LAN
  address and public key.

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
