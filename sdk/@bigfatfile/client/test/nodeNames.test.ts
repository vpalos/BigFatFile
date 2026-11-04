import { readFileSync } from "node:fs";
import { describe, expect, test } from "vitest";
import { nameKey, nameKeyUnicodeVersion, normalizeName } from "../src/index.js";

interface NameVectors {
  unicodeVersion: string;
  vectors: { name: string; input: string; normalized: string; key: string }[];
}

const vectors: NameVectors = JSON.parse(
  readFileSync(new URL("../../../test-vectors/names.json", import.meta.url), "utf8"),
);

describe("names match the shared vectors", () => {
  test("the Unicode version matches", () => {
    expect(nameKeyUnicodeVersion).toBe(vectors.unicodeVersion);
  });

  test.each(vectors.vectors)("$name: stored name", ({ input, normalized }) => {
    expect(normalizeName(input)).toBe(normalized);
  });

  test.each(vectors.vectors)("$name: key", ({ input, key }) => {
    expect(nameKey(input)).toBe(key);
  });

  test.each(["a\ud800b", "a\udc00", "\ud800", "\udc00\ud800"])("rejects an unpaired surrogate", (input) => {
    expect(() => normalizeName(input)).toThrow(RangeError);
    expect(() => nameKey(input)).toThrow(RangeError);
  });
});
