# BigFatFile Architecture: Transfer Protocol

How bytes move between clients, the Origin, the Replica and storage: drive uploads and downloads,
sends, and the transport layer. Referenced from `Architecture.md`.

## 1. Drive files

- Uploads use an upload session shaped like S3 multipart:
    1. `POST /uploads` with the manifest; the Origin returns the chunks the tenant does
       not have and, on S3, a presigned URL for each.
    2. The client uploads the missing chunks in parallel, one request each, to the Origin
       (`PUT /uploads/{id}/chunks/{hash}`) or directly to storage; the Origin verifies
       each chunk's hash on receipt.
    3. `POST /uploads/{id}/complete` attaches the manifest to the node, with `If-Match` on
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
- Downloads in the browser:
    - the web app assembles in the browser where a streaming download is possible
      (service worker, or the File System Access API), otherwise falls back to the
      server;
    - previews and playback (PDF, video) always go through the Origin:
      `GET /nodes/{id}/content` with range support, the Origin maps the range to chunks
      and streams only what is requested.
- Share links to drive files are served by the Origin (or the Replica on the LAN) as a
  streamed, assembled file;
    - multi-file links as a streamed zip.
- The web uploader chunks the file with FastCDC and hashes with BLAKE3 in WebAssembly,
  in a web worker;
    - the desktop client does the same natively.

## 2. Sends (transfers)

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

## 3. Transport

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
