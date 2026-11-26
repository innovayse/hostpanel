/**
 * Guard: no screen, store or helper writes out a list of languages. The platform's languages live in one place,
 * `ContentLanguage` (`src/locales/contentLanguage.ts`), with the interface languages as the explicit subset
 * `MessageLanguage` (`src/locales/messages.ts`); everything else derives from them (`STOREFRONT_LOCALES`,
 * `SUPPORTED_LOCALES`, `Object.values(ContentLanguage)`), so a language added there reaches every tab strip,
 * dropdown and settings row without another edit.
 *
 * The test reads every `.vue` and `.ts` file under `src`, with comments removed, and fails on an array literal
 * that holds two or more language codes (`['en', 'ru']`) or two or more members of a language enum
 * (`[ContentLanguage.En, ContentLanguage.Ru]`). A keyed map (`{ [ContentLanguage.En]: ... }`) is not a list: the
 * compiler makes it cover every language, so it is how per-language data is written. The files below are the
 * sources the lists derive from.
 */
import { readdirSync, readFileSync } from 'node:fs'
import { join, relative, sep } from 'node:path'
import { describe, expect, it } from 'vitest'

/** The admin source folder. */
const SRC_ROOT = join(__dirname, '..', '..', 'src')

/** File extensions that can carry a list. */
const SCANNED = /\.(vue|ts)$/

/** Language codes the platform has shipped. */
const CODES = 'en|ru|hy|de|es|fr'

/** The language enums whose members make a list. */
const ENUMS = 'ContentLanguage|MessageLanguage'

/** An array literal holding two or more quoted language codes. */
const CODE_LIST = new RegExp(`\\[[^\\[\\]{}]*['"](?:${CODES})['"][^\\[\\]{}]*['"](?:${CODES})['"][^\\[\\]{}]*\\]`)

/** An array literal holding two or more members of a language enum. */
const ENUM_LIST = new RegExp(`\\[[^\\[\\]{}]*\\b(?:${ENUMS})\\.\\w+[^\\[\\]{}]*\\b(?:${ENUMS})\\.\\w+[^\\[\\]{}]*\\]`)

/** The files that define the languages, relative to `src` with forward slashes, each with the reason. */
const SOURCES: Record<string, string> = {
  'locales/contentLanguage.ts': 'the enum every language list derives from',
  'locales/messages.ts': 'the explicit subset of languages the interface is translated into',
  'lib/storefrontTranslations.ts': 'builds STOREFRONT_LOCALES, the one derived list, from the enum (English first)',
}

/**
 * Removes block, HTML and `//` comments, so only code is scanned.
 *
 * @param source - File text.
 * @returns The text without comments.
 */
const withoutComments = (source: string): string =>
  source.replace(/\/\*[\s\S]*?\*\//g, '').replace(/<!--[\s\S]*?-->/g, '').replace(/(^|[^:'"`])\/\/[^\n]*/g, '$1')

/**
 * Lists the scanned files under a folder.
 *
 * @param dir - Folder to walk.
 * @returns Absolute paths of the `.vue` and `.ts` files.
 */
const filesUnder = (dir: string): string[] =>
  readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name)
    if (entry.isDirectory()) {
      return filesUnder(path)
    }
    return SCANNED.test(entry.name) ? [path] : []
  })

describe('language lists', () => {
  it('are derived from the shared source, never written out', () => {
    const offenders = filesUnder(SRC_ROOT)
      .map((path) => relative(SRC_ROOT, path).split(sep).join('/'))
      .filter((name) => !(name in SOURCES))
      .filter((name) => {
        const code = withoutComments(readFileSync(join(SRC_ROOT, name), 'utf-8'))
        return CODE_LIST.test(code) || ENUM_LIST.test(code)
      })

    expect(offenders).toEqual([])
  })

  it('catches a written-out list, so the guard cannot go quiet', () => {
    expect(CODE_LIST.test(`const all = ['en', 'ru']`)).toBe(true)
    expect(ENUM_LIST.test('const all = [ContentLanguage.En,\n  ContentLanguage.Ru]')).toBe(true)
    expect(CODE_LIST.test(`const one = ['en']`)).toBe(false)
    expect(ENUM_LIST.test('const keyed = { [ContentLanguage.En]: 1, [ContentLanguage.Ru]: 2 }')).toBe(false)
  })
})
