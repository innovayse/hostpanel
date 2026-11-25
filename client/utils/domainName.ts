/**
 * The rule for what a fully-qualified domain name looks like, checked in the browser before a name
 * is sent anywhere.
 *
 * It mirrors the API's `DomainNameRules` exactly (length, the labels a name is made of, the
 * characters a label may hold and the letters an extension needs), so a name the browser refuses is
 * one the API would refuse and a visitor reads the reason at once instead of after a round trip.
 * The API stays the source of truth: it checks again, and a change to its rule is made here too
 * (`tests/utils/domainName.test.ts` holds the same table of names as the API's rule tests).
 *
 * @module utils/domainName
 */

/** Longest a whole name may be, dots included. */
export const MAX_DOMAIN_NAME_LENGTH = 253

/** Longest a single label (the text between two dots) may be. */
export const MAX_DOMAIN_LABEL_LENGTH = 63

/** The fewest labels a name needs: one for the name and one for the extension. */
const MIN_DOMAIN_LABELS = 2

/** The fewest letters an extension (the last label) needs. */
const MIN_DOMAIN_EXTENSION_LENGTH = 2

/** The character that separates two labels. */
const LABEL_SEPARATOR = '.'

/** The one punctuation mark a label may hold, and never at its start or end. */
const HYPHEN = '-'

/** The characters a label is made of: ASCII letters, digits and hyphens. */
const LABEL_CHARACTERS = /^[A-Za-z0-9-]+$/

/** The characters an extension is made of: ASCII letters. */
const EXTENSION_CHARACTERS = /^[A-Za-z]+$/

/**
 * Whether one label of a name is well-formed.
 *
 * @param label - The text between two dots.
 * @returns True when the label is 1 to {@link MAX_DOMAIN_LABEL_LENGTH} ASCII letters, digits and
 * hyphens and neither starts nor ends with a hyphen.
 */
export const isValidDomainLabel = (label: string): boolean =>
  label.length >= 1
  && label.length <= MAX_DOMAIN_LABEL_LENGTH
  && !label.startsWith(HYPHEN)
  && !label.endsWith(HYPHEN)
  && LABEL_CHARACTERS.test(label)

/**
 * Whether the last label of a name is an extension (a top-level domain).
 *
 * @param label - The last label of a name.
 * @returns True when it is at least {@link MIN_DOMAIN_EXTENSION_LENGTH} ASCII letters: a number is no
 * extension, so `example.123` is never a name.
 */
export const isValidDomainExtension = (label: string): boolean =>
  label.length >= MIN_DOMAIN_EXTENSION_LENGTH && EXTENSION_CHARACTERS.test(label)

/**
 * Whether the part of a name before its extension is well-formed: every dot-separated label of it
 * passes {@link isValidDomainLabel}. The extension is the operator's own price table, so a search
 * that appends it can judge what the visitor typed without waiting for that table to load.
 *
 * @param base - The text typed, without an extension (`my-shop`, or `shop.example` for a nested name).
 * @returns True when no label of it is malformed.
 */
export const isValidDomainBase = (base: string): boolean =>
  base.split(LABEL_SEPARATOR).every(isValidDomainLabel)

/**
 * Whether a text is a well-formed fully-qualified domain name.
 *
 * @param name - The name exactly as given: white space around it is not part of a name, so a caller
 * that accepts a padded input trims it first.
 * @returns True when the text is at most {@link MAX_DOMAIN_NAME_LENGTH} characters, has a name and an
 * extension, every label passes {@link isValidDomainLabel} and the last passes
 * {@link isValidDomainExtension}; false for a missing or blank text.
 */
export const isValidDomainName = (name: string | null | undefined): boolean => {
  if (!name || name.length > MAX_DOMAIN_NAME_LENGTH) {
    return false
  }
  const labels = name.split(LABEL_SEPARATOR)
  const extension = labels[labels.length - 1] ?? ''
  return labels.length >= MIN_DOMAIN_LABELS && labels.every(isValidDomainLabel) && isValidDomainExtension(extension)
}
