import { caseFolding } from "./caseFolding.g.js";

export { unicodeVersion as nameKeyUnicodeVersion } from "./caseFolding.g.js";

/**
 * The name as it is stored: in NFC, so that the composed and the decomposed spelling of a
 * name are one name. It must match NodeNames.Normalize in BigFatFile.Contracts, which the
 * shared test vectors check.
 *
 * @throws {RangeError} The name contains an unpaired surrogate.
 */
export function normalizeName(name: string): string {
  if (!name.isWellFormed()) {
    throw new RangeError("The name contains an unpaired surrogate.");
  }

  return name.normalize("NFC");
}

/**
 * The name key: two siblings may not have names with the same key. It is the name in NFC,
 * simple-case-folded with the pinned Unicode table, then in NFC again. It must match
 * NodeNames.Key in BigFatFile.Contracts, which the shared test vectors check.
 *
 * @throws {RangeError} The name contains an unpaired surrogate.
 */
export function nameKey(name: string): string {
  let folded = "";
  for (const char of normalizeName(name)) {
    const codePoint = char.codePointAt(0)!;
    folded += String.fromCodePoint(caseFolding.get(codePoint) ?? codePoint);
  }

  return folded.normalize("NFC");
}
