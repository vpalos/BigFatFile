import createClient, { type Client, type Middleware } from "openapi-fetch";
import type { paths } from "./generated/api.js";

export type BffClient = Client<paths>;

export interface BffClientOptions {
  /** The Origin or Replica address, for example "https://files.example.com". */
  baseUrl: string;
  /** Returns the current access token. Without it, requests carry no token, which suits the web app's session cookie. */
  accessToken?: () => string | undefined | Promise<string | undefined>;
  fetch?: typeof globalThis.fetch;
}

export function createBffClient(options: BffClientOptions): BffClient {
  const client = createClient<paths>({
    baseUrl: options.baseUrl,
    ...(options.fetch ? { fetch: options.fetch } : {}),
  });

  const { accessToken } = options;
  if (accessToken) {
    const bearer: Middleware = {
      async onRequest({ request }) {
        const token = await accessToken();
        if (token) {
          request.headers.set("Authorization", `Bearer ${token}`);
        }
        return request;
      },
    };
    client.use(bearer);
  }

  return client;
}
