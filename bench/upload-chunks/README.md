# Upload chunk-size benchmark

Measures upload throughput for a fixed amount of data split into chunks of a chosen
size, sent over HTTP/1.1 or HTTP/2, with a chosen number of requests in flight.
Plain Node, no dependencies. Runs in Docker with the official Node image, or directly
with Node 20 or later.

## Files

- `server.mjs`: the sink. Reads and discards every `PUT` body. HTTP/1.1 on port 8081,
  HTTP/2 (plaintext h2c) on port 8082. `--delay <ms>` waits before each response to
  imitate the round-trip time of a remote storage endpoint.
- `client.mjs`: one run. `--proto h1|h2`, `--chunk <MB>`, `--total <MB>`,
  `--concurrency <n>`, `--host`, `--port`. Prints one JSON line.
- `matrix.mjs`: runs protocols x chunk sizes x concurrency levels and prints a table.
  `--chunks 4,64`, `--concurrency 1,8`, `--total 1024`, `--host`.
- `docker-compose.yml`: a `server` service and a one-off `client` service, both from
  `node:22-alpine` with this folder mounted. No build step.

## Run with Docker

Same machine, both sides in containers:

```sh
docker compose up -d server
docker compose run --rm client
docker compose down
```

Arguments after `client` go to `matrix.mjs`:

```sh
docker compose run --rm client --chunks 4,16,64 --concurrency 1,4,8 --total 512
```

Simulated round trip (the server waits before each response):

```sh
DELAY_MS=30 docker compose up -d server
docker compose run --rm client
```

Two machines (the realistic case):

```sh
# on the receiving machine
docker compose up -d server
# on the sending machine (HOST is the receiver's IP; --no-deps skips the local server)
HOST=192.168.1.20 docker compose run --rm --no-deps client
```

The server publishes ports 8081 and 8082 on the host, so the receiver must allow them
through its firewall.

## Run with Node directly

```sh
node server.mjs            # add --delay 30 to simulate a round trip
node matrix.mjs            # add --host <ip> from a second machine
```

Real latency and bandwidth limits on Linux, applied on the sending host (works for the
Docker case too, since containers share the host's network path):

```sh
sudo tc qdisc add dev <interface> root netem delay 15ms rate 100mbit
# ... run the matrix ...
sudo tc qdisc del dev <interface> root
```

## What to look at

- Sequential (1 in flight): throughput falls as chunks get smaller, because each
  request pays one round trip. This is the case the chunk size matters for.
- 8 in flight: the round trips overlap and both chunk sizes reach the link speed.
- HTTP/1.1 uses one TCP connection per request in flight; HTTP/2 uses one connection
  with one stream per request. Both reach the same speed once the HTTP/2 flow-control
  windows are large enough (the server and client set 16 MB stream windows and 64 MB
  connection windows; the Node defaults stall bulk uploads).

Example on loopback, 1 GB, `--delay 30`:

```
proto  chunk MB  in flight     MB/s
   h1         4          1    121.3
   h1         4          8    944.4
   h1        64          1   1055.8
   h1        64          8   1567.6
   h2         4          1    119.1
   h2         4          8    738.6
   h2        64          1   1051.8
   h2        64          8   2071.8
```

With one request at a time, 4 MB chunks are limited to one chunk per round trip
(4 MB / 33 ms). With eight in flight the same chunks run eight times faster. On a real
uplink of 10 to 100 Mbit/s the link, not the round trip, is the limit in every row.

## Notes

- Node's HTTP/2 implementation caps memory per session at 10 MB by default
  (`maxSessionMemory`); the scripts raise it, or a few 4 MB chunks in flight are
  refused with `ENHANCE_YOUR_CALM`.
- The 64 MB rows at 8 in flight hold 512 MB of request buffers in the client; that is
  the memory cost of large chunks, not a quirk of the benchmark. Give the client
  container enough memory for the sizes you test.
- Loopback inside Docker Desktop (Windows, macOS) goes through a virtual machine and
  is slower than native loopback; compare rows against each other, not against
  numbers from another machine.
