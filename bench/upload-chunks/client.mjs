// Uploads a fixed amount of data as equal chunks, N requests at a time,
// over HTTP/1.1 (keep-alive connections) or HTTP/2 (streams on one session).
// Prints throughput and per-request timing as one JSON line.

import http from "node:http";
import http2 from "node:http2";
import { parseArgs } from "node:util";

const MB = 1024 * 1024;

export function parseClientArgs(argv) {
  const { values } = parseArgs({
    args: argv,
    options: {
      host: { type: "string", default: "127.0.0.1" },
      port: { type: "string" },
      proto: { type: "string", default: "h1" },
      chunk: { type: "string", default: "4" },
      total: { type: "string", default: "1024" },
      concurrency: { type: "string", default: "8" },
    },
  });
  const proto = values.proto;
  return {
    host: values.host,
    port: Number(values.port ?? (proto === "h2" ? 8082 : 8081)),
    proto,
    chunkBytes: Number(values.chunk) * MB,
    totalBytes: Number(values.total) * MB,
    concurrency: Number(values.concurrency),
  };
}

function uploadH1(cfg, body, index) {
  return new Promise((resolve, reject) => {
    const req = http.request(
      {
        host: cfg.host,
        port: cfg.port,
        method: "PUT",
        path: `/chunk/${index}`,
        agent: cfg.agent,
        headers: { "content-length": body.length },
      },
      (res) => {
        res.resume();
        res.on("end", () => (res.statusCode === 200 ? resolve() : reject(new Error(`status ${res.statusCode}`))));
      }
    );
    req.on("error", reject);
    req.end(body);
  });
}

function uploadH2(cfg, body, index) {
  return new Promise((resolve, reject) => {
    const stream = cfg.session.request({
      ":method": "PUT",
      ":path": `/chunk/${index}`,
      "content-length": body.length,
    });
    stream.on("response", (headers) => {
      if (headers[":status"] !== 200) reject(new Error(`status ${headers[":status"]}`));
    });
    stream.on("data", () => {});
    stream.on("end", resolve);
    stream.on("error", reject);
    stream.end(body);
  });
}

export async function runUpload(cfg) {
  const body = Buffer.alloc(cfg.chunkBytes, 0x5a);
  const count = Math.ceil(cfg.totalBytes / cfg.chunkBytes);
  const upload = cfg.proto === "h2" ? uploadH2 : uploadH1;

  if (cfg.proto === "h2") {
    cfg.session = http2.connect(`http://${cfg.host}:${cfg.port}`, {
      maxSessionMemory: 4096,
      settings: { initialWindowSize: 16 * MB },
    });
    await new Promise((resolve, reject) => {
      cfg.session.once("connect", resolve);
      cfg.session.once("error", reject);
    });
    cfg.session.setLocalWindowSize(64 * MB);
  } else {
    cfg.agent = new http.Agent({ keepAlive: true, maxSockets: cfg.concurrency });
  }

  const durations = [];
  let next = 0;
  const started = process.hrtime.bigint();

  async function worker() {
    while (next < count) {
      const index = next++;
      const t0 = process.hrtime.bigint();
      await upload(cfg, body, index);
      durations.push(Number(process.hrtime.bigint() - t0) / 1e6);
    }
  }
  await Promise.all(Array.from({ length: cfg.concurrency }, worker));

  const seconds = Number(process.hrtime.bigint() - started) / 1e9;
  if (cfg.session) cfg.session.close();
  if (cfg.agent) cfg.agent.destroy();

  durations.sort((a, b) => a - b);
  return {
    proto: cfg.proto,
    chunkMB: cfg.chunkBytes / MB,
    totalMB: cfg.totalBytes / MB,
    concurrency: cfg.concurrency,
    requests: count,
    seconds: Number(seconds.toFixed(2)),
    throughputMBps: Number((cfg.totalBytes / MB / seconds).toFixed(1)),
    requestMsMedian: Number(durations[Math.floor(durations.length / 2)].toFixed(1)),
    requestMsP95: Number(durations[Math.floor(durations.length * 0.95)].toFixed(1)),
  };
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const cfg = parseClientArgs(process.argv.slice(2));
  runUpload(cfg).then((r) => console.log(JSON.stringify(r)));
}
