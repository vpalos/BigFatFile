// Runs the whole comparison and prints a table:
// protocols {h1, h2} x chunk sizes {4, 64} MB x concurrency levels.

import { parseArgs } from "node:util";
import { runUpload } from "./client.mjs";

const MB = 1024 * 1024;

const { values: opts } = parseArgs({
  options: {
    host: { type: "string", default: "127.0.0.1" },
    "h1-port": { type: "string", default: "8081" },
    "h2-port": { type: "string", default: "8082" },
    total: { type: "string", default: "1024" },
    chunks: { type: "string", default: "4,64" },
    concurrency: { type: "string", default: "1,8" },
  },
});

const chunkSizes = opts.chunks.split(",").map(Number);
const concurrencies = opts.concurrency.split(",").map(Number);
const rows = [];

for (const proto of ["h1", "h2"]) {
  for (const chunkMB of chunkSizes) {
    for (const concurrency of concurrencies) {
      const result = await runUpload({
        host: opts.host,
        port: Number(proto === "h2" ? opts["h2-port"] : opts["h1-port"]),
        proto,
        chunkBytes: chunkMB * MB,
        totalBytes: Number(opts.total) * MB,
        concurrency,
      });
      rows.push(result);
      console.error(
        `${proto} ${String(chunkMB).padStart(3)} MB x${concurrency}: ${result.throughputMBps} MB/s`
      );
    }
  }
}

const header = ["proto", "chunk MB", "in flight", "requests", "seconds", "MB/s", "req ms (median)", "req ms (p95)"];
const lines = rows.map((r) => [
  r.proto,
  r.chunkMB,
  r.concurrency,
  r.requests,
  r.seconds,
  r.throughputMBps,
  r.requestMsMedian,
  r.requestMsP95,
]);
const widths = header.map((h, i) => Math.max(h.length, ...lines.map((l) => String(l[i]).length)));
const fmt = (cells) => cells.map((c, i) => String(c).padStart(widths[i])).join("  ");
console.log(fmt(header));
console.log(fmt(widths.map((w) => "-".repeat(w))));
for (const line of lines) console.log(fmt(line));
