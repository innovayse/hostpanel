/**
 * Guard: no component, composable, server route or config writes out a list of languages. The portal's
 * languages live in one place, the `AppLocale` enum (`types/applocale.ts`); everything else derives from it
 * (`APP_LOCALES`, `Object.values(AppLocale)`), so a language added there reaches the router, the switcher,
 * the settings rows and the runtime config without another edit.
 *
 * The test reads every `.vue` and `.ts` file of the source folders and `nuxt.config.ts`, with comments removed,
 * and fails on an array literal that holds two or more language codes (`['en', 'ru']`) or two or more
 * `AppLocale` members. A keyed map (`{ [AppLocale.En]: ... }`) is not a list: the compiler makes it cover every
 * language, so it is how per-language data is written.
 *
 * @module tests/lint/noHandWrittenLanguageList.test
 */

import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { join, relative, sep } from 'node:path'
import { describe, expect, it } from 'vitest'

/** The client project root. */
const ROOT = join(__dirname, '..', '..')

/** The folders that hold source, relative to the root; tests, e2e and the locale catalogues are not source. */
const SOURCE_FOLDERS = ['components', 'composables', 'constants', 'layouts', 'middleware', 'pages', 'plugins', 'server', 'stores', 'templates', 'types', 'utils', 'lib']

/** Loose source files at the root. */
const SOURCE_FILES = ['nuxt.config.ts', 'app.vue', 'error.vue']

/** Language codes the portal ships. */
const CODES = 'en|ru|hy|de|es|fr'

/** An array literal holding two or more quoted language codes. */
const CODE_LIST = new RegExp(`\\[[^\\[\\]{}]*['"](?:${CODES})['"][^\\[\\]{}]*['"](?:${CODES})['"][^\\[\\]{}]*\\]`)

/** An array literal holding two or more `AppLocale` members. */
const ENUM_LIST = /\[[^[\]{}]*\bAppLocale\.\w+[^[\]{}]*\bAppLocale\.\w+[^[\]{}]*\]/

/**
 * Removes block, HTML and `//` comments, so only code is scanned.
 *
 * @param source File text.
 * @returns The text without comments.
 */
const withoutComments = (source: string): string =>
  source.replace(/\/\*[\s\S]*?\*\//g, '').replace(/<!--[\s\S]*?-->/g, '').replace(/(^|[^:'"`])\/\/[^\n]*/g, '$1')

/**
 * Lists the scanned files under a folder.
 *
 * @param dir Folder to walk.
 * @returns Absolute paths of the `.vue` and `.ts` files.
 */
const filesUnder = (dir: string): string[] => {
  if (!existsSync(dir)) {
    return []
  }
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name)
    if (entry.isDirectory()) {
      return filesUnder(path)
    }
    return /\.(vue|ts)$/.test(entry.name) ? [path] : []
  })
}

describe('language lists', () => {
  it('are derived from AppLocale, never written out', () => {
    const files = [
      ...SOURCE_FOLDERS.flatMap(folder => filesUnder(join(ROOT, folder))),
      ...SOURCE_FILES.map(file => join(ROOT, file)).filter(existsSync)
    ]
    const offenders = files
      .map(path => relative(ROOT, path).split(sep).join('/'))
      .filter((name) => {
        const code = withoutComments(readFileSync(join(ROOT, name), 'utf-8'))
        return CODE_LIST.test(code) || ENUM_LIST.test(code)
      })

    expect(offenders).toEqual([])
  })

  it('catches a written-out list, so the guard cannot go quiet', () => {
    expect(CODE_LIST.test('const all = [\'en\', \'ru\']')).toBe(true)
    expect(ENUM_LIST.test('const all = [AppLocale.En,\n  AppLocale.Ru]')).toBe(true)
    expect(CODE_LIST.test('const one = [\'en\']')).toBe(false)
    expect(ENUM_LIST.test('const keyed = { [AppLocale.En]: 1, [AppLocale.Ru]: 2 }')).toBe(false)
  })
})
