/**
 * Locale parity for the customer portal: every module file under `locales/en` must exist in
 * `ru` and `hy`, carry exactly the same keys, hold only non-empty strings (or the string
 * arrays the pages read with `tm()`), and keep the same `{placeholders}` in every language.
 */

import { readdirSync, readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { APP_LOCALES } from '~/constants/appLocales'
import { AppLocale } from '~/types/applocale'

/** The languages the portal ships; plugins/i18n.ts loads all of them. */
const LOCALES = APP_LOCALES

/** The locale directory every other language is compared with. */
const REFERENCE: string = AppLocale.En

/**
 * Absolute path of a locale directory.
 *
 * @param locale - Locale directory name.
 * @returns The directory path.
 */
const dirOf = (locale: string): string => resolve(import.meta.dirname, '../../locales', locale)

/**
 * Reads and parses one module file of one language.
 *
 * @param locale - Locale directory name.
 * @param file - Module file name, e.g. `client.json`.
 * @returns The parsed message tree.
 */
const load = (locale: string, file: string): unknown =>
  JSON.parse(readFileSync(`${dirOf(locale)}/${file}`, 'utf8'))

/**
 * Flattens a message tree into dotted keys. Arrays are leaves: the portal reads them whole
 * with `tm()`.
 *
 * @param node - A message tree or a leaf value.
 * @param prefix - Dotted path accumulated so far.
 * @param out - Collector for the flattened entries.
 * @returns Every leaf keyed by its dotted path.
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
 * repeats a name once per form (Russian has three forms where English has two), so a name counts once.
 *
 * @param message - A message string.
 * @returns The placeholder names, sorted and comma-joined.
 */
const placeholders = (message: string): string =>
  [...new Set([...message.matchAll(/\{([A-Za-z_]\w*)\}/g)].map((match) => match[1]))].sort().join(',')

/** How a message spells an at-sign vue-i18n must not read as a linked message. */
const LITERAL_AT = "{'@'}"

/**
 * Module files a language does not translate yet, which fall back to English at runtime (`fallbackLocale`).
 * The long legal texts are left out of the German translation until a person who knows the law of the
 * operator's country has written them; a machine translation of a privacy policy would mislead. The list is
 * `locales/untranslated.json`, which `scripts/lint-i18n.mjs` reads too.
 */
const UNTRANSLATED: Partial<Record<AppLocale, readonly string[]>> = JSON.parse(
  readFileSync(resolve(import.meta.dirname, '../../locales/untranslated.json'), 'utf8')
)

/**
 * Tells whether a language ships a module file of the reference.
 *
 * @param locale - Locale directory name.
 * @param file - Module file name.
 * @returns False when the file is listed in {@link UNTRANSLATED} for the language.
 */
const isTranslated = (locale: string, file: string): boolean => !(UNTRANSLATED[locale as AppLocale] ?? []).includes(file)

/** The languages compared against the reference. */
const TRANSLATIONS = LOCALES.filter((locale) => locale !== REFERENCE)

/** Module files of the reference language. */
const FILES = readdirSync(dirOf(REFERENCE)).filter((name) => name.endsWith('.json')).sort()

describe('portal locale parity', () => {
  it('has a locale folder for each AppLocale and no other', () => {
    const folders = readdirSync(resolve(import.meta.dirname, '../../locales'), { withFileTypes: true })
      .filter((entry) => entry.isDirectory())
      .map((entry) => entry.name)
      .sort()

    expect(folders).toEqual([...LOCALES].sort())
  })

  it.each(TRANSLATIONS)('%s has exactly the module files of en', (locale) => {
    const files = readdirSync(dirOf(locale)).filter((name) => name.endsWith('.json')).sort()

    expect(files).toEqual(FILES.filter((file) => isTranslated(locale, file)))
  })

  describe.each(FILES)('%s', (file) => {
    const reference = flatten(load(REFERENCE, file))

    it.each(TRANSLATIONS.filter((locale) => isTranslated(locale, file)))('%s has exactly the keys of en', (locale) => {
      const actual = flatten(load(locale, file))
      const expected = Object.keys(reference)

      expect(Object.keys(actual).filter((key) => !expected.includes(key))).toEqual([])
      expect(expected.filter((key) => !(key in actual))).toEqual([])
    })

    it.each(LOCALES.filter((locale) => isTranslated(locale, file)))('%s holds no empty message', (locale) => {
      const empty = Object.entries(flatten(load(locale, file)))
        .filter(([, value]) => value === '' || value === null || value === undefined || (typeof value === 'string' && value.trim() === ''))
        .map(([key]) => key)

      expect(empty).toEqual([])
    })

    it.each(LOCALES.filter((locale) => isTranslated(locale, file)))('%s writes every at-sign as a literal', (locale) => {
      // vue-i18n reads a bare @ as the start of a linked message, so `name@example.com` throws
      // when it is compiled and the component that shows it renders nothing.
      const bare = Object.entries(flatten(load(locale, file)))
        .filter(([, value]) => typeof value === 'string' && value.replaceAll(LITERAL_AT, '').includes('@'))
        .map(([key]) => key)

      expect(bare).toEqual([])
    })

    it.each(TRANSLATIONS.filter((locale) => isTranslated(locale, file)))('%s keeps the placeholders of en', (locale) => {
      const actual = flatten(load(locale, file))
      const mismatched = Object.entries(reference)
        .filter(([key, value]) => {
          const other = actual[key]
          return typeof value === 'string' && typeof other === 'string' && placeholders(value) !== placeholders(other)
        })
        .map(([key]) => key)

      expect(mismatched).toEqual([])
    })
  })
})
