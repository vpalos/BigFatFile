# Upload chunk-size benchmark

Measures upload throughput for a fixed amount of data split into chunks of a chosen
size, sent over HTTP/1.1 or HTTP/2, with a chosen number of requests in flight.
Plain Node, no dependencies, Node 20 or later.

## Files

- `server.mjs`: the sink. Reads and discards every `PUT` body. HTTP/1.1 on port 8081,
  HTTP/2 (plaintext h2c) on port 8082. `--delay <ms>` waits before each response to
  imitate the round-trip time of a remote storage endpoint.
- `client.mjs`: one run. `--proto h1|h2`, `--chunk <MB>`, `--total <MB>`,
  `--concurrency <n>`, `--host`, `--port`. Prints one JSON line.
- `matrix.mjs`: runs protocols x chunk sizes x concurrency levels and prints a table.
  `--chunks 4,64`, `--concurrency 1,8`, `--total 1024`, `--host`.

## Run

Same machine (loopback):

```sh
node server.mjs
node matrix.mjs
```

Two machines (the realistic case):

```sh
# on the receiving machine
node server.mjs
# on the sending machine
node matrix.mjs --host <receiver-ip>
```

Simulated round trip without a second machine:

```sh
node server.mjs --delay 30
node matrix.mjs
```

On Linux, real latency and bandwidth limits can be added with `tc`:

```sh
sudo tc qdisc add dev lo root netem delay 15ms rate 100mbit
# ... run the matrix ...
sudo tc qdisc del dev lo root
```

## What to look at

- Sequential (1 in flight): throughput falls as chunks get smaller, because each
  request pays one round trip. This is the case the chunk size matters for.
- 8 in flight: the round trips overlap and both chunk sizes reach the link speed.
- HTTP/1.1 uses one TCP connection per request in flight; HTTP/2 uses one connection
  with one stream per request. Both reach the same speed once the HTTP/2 flow-control
  windows are large enough (the server and client set 16 MB stream windows and 64 MB
  connection windows; the Node defaults stall bulk uploads).

Example from a container on loopback, 1 GB, `--delay 30`:

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
  the memory cost of large chunks, not a quirk of the benchmark.
