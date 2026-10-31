// Signs in to a BigFatFile Origin the way a CLI does, then calls the API with the typed client.
// 1. Opens the browser at the Origin's authorization endpoint (authorization code with PKCE).
// 2. Receives the code on a loopback address and exchanges it for tokens.
// 3. Calls GET /api/v1/me.
//
// Usage, with `./bff dev up` running: pnpm --filter @bigfatfile/example-sign-in start
// Set BFF_URL to use another Origin than http://localhost:8080.

import { spawn } from "node:child_process";
import { createHash, randomBytes } from "node:crypto";
import { createServer } from "node:http";
import { createBffClient, type Me } from "@bigfatfile/client";

const origin = process.env.BFF_URL ?? "http://localhost:8080";
const clientId = "bff-dev-cli";
const redirectUri = "http://127.0.0.1:53682/callback";

const verifier = randomBytes(32).toString("base64url");
const challenge = createHash("sha256").update(verifier).digest("base64url");
const state = randomBytes(16).toString("base64url");

const authorizeUrl = new URL("/connect/authorize", origin);
authorizeUrl.search = new URLSearchParams({
  client_id: clientId,
  redirect_uri: redirectUri,
  response_type: "code",
  scope: "openid profile email offline_access",
  state,
  code_challenge: challenge,
  code_challenge_method: "S256",
}).toString();

const code = await new Promise<string>((resolve, reject) => {
  const server = createServer((request, response) => {
    const url = new URL(request.url ?? "/", redirectUri);
    if (url.pathname !== "/callback") {
      response.writeHead(404).end();
      return;
    }

    const received = url.searchParams.get("code");
    const ok = url.searchParams.get("state") === state && received !== null;
    response.writeHead(ok ? 200 : 400, { "Content-Type": "text/plain" });
    response.end(ok ? "Signed in. You can close this tab." : "The sign-in failed. See the terminal.");
    server.close();
    if (ok) {
      resolve(received);
    } else {
      reject(new Error(`The callback had no code or the wrong state: ${url.search}`));
    }
  });

  server.listen(53682, "127.0.0.1", () => {
    console.log(`Opening the browser at:\n${authorizeUrl}\n`);
    openBrowser(authorizeUrl.toString());
  });
});

const tokenResponse = await fetch(new URL("/connect/token", origin), {
  method: "POST",
  body: new URLSearchParams({
    grant_type: "authorization_code",
    client_id: clientId,
    redirect_uri: redirectUri,
    code,
    code_verifier: verifier,
  }),
});
if (!tokenResponse.ok) {
  throw new Error(`The token request failed: ${tokenResponse.status} ${await tokenResponse.text()}`);
}
const tokens = (await tokenResponse.json()) as { access_token: string };

const client = createBffClient({ baseUrl: origin, accessToken: () => tokens.access_token });
const { data, error } = await client.GET("/api/v1/me");
if (error || !data) {
  throw new Error(`GET /api/v1/me failed: ${JSON.stringify(error)}`);
}

const me: Me = data;
console.log(`Signed in as ${me.email}.`);
for (const tenant of me.tenants) {
  console.log(`- ${tenant.name}: ${tenant.role}`);
}

function openBrowser(url: string): void {
  const command = process.platform === "darwin" ? "open" : process.platform === "win32" ? "explorer" : "xdg-open";
  spawn(command, [url], { stdio: "ignore", detached: true })
    .on("error", () => console.log("Open the address above in a browser."))
    .unref();
}
