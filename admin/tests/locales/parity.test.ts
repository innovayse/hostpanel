/**
 * Locale parity for the admin panel: `en`, `ru`, `hy`, `de` and `es` must carry exactly the same keys,
 * every value must be a non-empty string, and the `{placeholders}` of a sentence must be the
 * same in every language, so a translation can never silently drop a value the code passes.
 */

import { describe, expect, it } from 'vitest'
import { messages } from '@/locales/messages'

/** The catalogues under test (every area merged), keyed by locale code. */
const CATALOGUES = { en: messages.en, ru: messages.ru, hy: messages.hy, de: messages.de, es: messages.es } as const

/**
 * Flattens a nested message tree into dotted keys.
 *
 * @param node - A message tree or a leaf value.
 * @param prefix - Dotted path accumulated so far.
 * @param out - Collector for the flattened entries.
 * @returns Every leaf of the tree keyed by its dotted path.
 */
const flatten = (node: unknown, prefix = '', out: Record<string, unknown> = {}): Record<string, unknown> => {
  if (node !== null && typeof node === 'object' && !Array.isArray(node)) {
    for (const [key, value] of Object.entries(node as Record<string, unknown>)) {
      flatten(value, prefix ? `${prefix}.${key}` : key, out)
    }
    return out
  }
  out[prefix] = node
  return out
}

/**
 * Lists the distinct `{named}` placeholders of a message in a stable order. A plural message
 * repeats its placeholder once per form and the number of forms differs per language (`ru` has
 * three, `en` two), so each name is counted once.
 *
 * @param message - A message string.
 * @returns The placeholder names, sorted and comma-joined.
 */
const placeholders = (message: string): string =>
  [...new Set([...message.matchAll(/\{([A-Za-z_]\w*)\}/g)].map((match) => match[1]))].sort().join(',')

/** Flattened view of each catalogue. */
const FLAT = {
  en: flatten(CATALOGUES.en),
  ru: flatten(CATALOGUES.ru),
  hy: flatten(CATALOGUES.hy),
  de: flatten(CATALOGUES.de),
  es: flatten(CATALOGUES.es),
}

describe('admin locale parity', () => {
  it.each(['ru', 'hy', 'de', 'es'] as const)('%s has exactly the keys of en', (locale) => {
    const reference = Object.keys(FLAT.en).sort()
    const actual = Object.keys(FLAT[locale]).sort()

    expect(actual.filter((key) => !reference.includes(key))).toEqual([])
    expect(reference.filter((key) => !actual.includes(key))).toEqual([])
  })

  it.each(Object.keys(CATALOGUES))('%s has only non-empty string values', (locale) => {
    const bad = Object.entries(FLAT[locale as keyof typeof FLAT])
      .filter(([, value]) => typeof value !== 'string' || value.trim() === '')
      .map(([key]) => key)

    expect(bad).toEqual([])
  })

  it.each(['ru', 'hy', 'de', 'es'] as const)('%s keeps the placeholders of en', (locale) => {
    const mismatched = Object.entries(FLAT.en)
      .filter(([key, value]) => {
        const other = FLAT[locale][key]
        return typeof value === 'string' && typeof other === 'string' && placeholders(value) !== placeholders(other)
      })
      .map(([key]) => key)

    expect(mismatched).toEqual([])
  })
})
