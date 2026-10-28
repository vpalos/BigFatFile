// Upload sink for the chunk benchmark.
// Listens with HTTP/1.1 on one port and with HTTP/2 (plaintext, h2c) on another.
// Every PUT body is read and discarded. An optional delay before each response
// imitates the round-trip time of a remote storage endpoint.

import http from "node:http";
import http2 from "node:http2";
import { parseArgs } from "node:util";

const { values: opts } = parseArgs({
  options: {
    "h1-port": { type: "string", default: "8081" },
    "h2-port": { type: "string", default: "8082" },
    delay: { type: "string", default: "0" },
  },
});

const delayMs = Number(opts.delay);
let totalBytes = 0;
let totalRequests = 0;

function drain(stream, onDone) {
  let bytes = 0;
  stream.on("data", (chunk) => {
    bytes += chunk.length;
  });
  stream.on("end", () => {
    totalBytes += bytes;
    totalRequests += 1;
    if (delayMs > 0) setTimeout(() => onDone(bytes), delayMs);
    else onDone(bytes);
  });
}

function handleStats(res) {
  res.setHeader("content-type", "application/json");
  res.end(JSON.stringify({ totalBytes, totalRequests }));
}

const h1 = http.createServer((req, res) => {
  if (req.method === "GET" && req.url === "/stats") return handleStats(res);
  if (req.method !== "PUT") {
    res.statusCode = 405;
    return res.end();
  }
  drain(req, (bytes) => {
    res.statusCode = 200;
    res.end(String(bytes));
  });
});

// Larger windows let one HTTP/2 connection carry bulk uploads at full speed.
// maxSessionMemory defaults to 10 MB, which is less than a few chunks in flight.
const h2 = http2.createServer({
  maxSessionMemory: 4096,
  settings: { initialWindowSize: 16 * 1024 * 1024 },
});
h2.on("stream", (stream, headers) => {
  const method = headers[":method"];
  const path = headers[":path"];
  if (method === "GET" && path === "/stats") {
    stream.respond({ ":status": 200, "content-type": "application/json" });
    return stream.end(JSON.stringify({ totalBytes, totalRequests }));
  }
  if (method !== "PUT") {
    stream.respond({ ":status": 405 });
    return stream.end();
  }
  drain(stream, (bytes) => {
    stream.respond({ ":status": 200 });
    stream.end(String(bytes));
  });
});
h2.on("session", (session) => {
  session.setLocalWindowSize(64 * 1024 * 1024);
});

h1.listen(Number(opts["h1-port"]), () =>
  console.log(`HTTP/1.1 sink on port ${opts["h1-port"]}`)
);
h2.listen(Number(opts["h2-port"]), () =>
  console.log(`HTTP/2 (h2c) sink on port ${opts["h2-port"]}`)
);
if (delayMs > 0) console.log(`Responding after a ${delayMs} ms delay`);
