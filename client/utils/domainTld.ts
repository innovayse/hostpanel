/**
 * The extension part of a domain name, shared by every screen that looks a name up in the TLD
 * price table.
 *
 * @module utils/domainTld
 */

/**
 * The extension of a domain name.
 *
 * @param domain - Full domain name (e.g. "example.com").
 * @returns Everything after the first dot, without the dot (e.g. "com"); the whole input when it
 * has no dot, and an empty string when the dot is last.
 */
export const tldOf = (domain: string): string => domain.slice(domain.indexOf('.') + 1)
