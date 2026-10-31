export { createBffClient, type BffClient, type BffClientOptions } from "./client.js";
export { nameKey, nameKeyUnicodeVersion } from "./contracts/nameKey.js";
export type { components, operations, paths } from "./generated/api.js";

import type { components } from "./generated/api.js";

export type Me = components["schemas"]["Me"];
export type Meta = components["schemas"]["Meta"];
