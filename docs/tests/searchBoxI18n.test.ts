/**
 * Unit tests for the texts of the search modal (`LocalSearchBox.vue`): every string comes from the language
 * files through `useUi`, so the component carries no English of its own, and all three languages have the
 * keys the component reads.
 */
import { readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { LOCALE_KEYS } from '../.vitepress/i18n/locales'
import { UI_STRINGS } from '../.vitepress/i18n/ui'

/** Source of the search modal component. */
const source = readFileSync(
  resolve(dirname(fileURLToPath(import.meta.url)), '../.vitepress/theme/components/LocalSearchBox.vue'),
  'utf-8',
).replace(/\r\n/g, '\n')

/** The `search.*` keys the component template and script read, besides the suggested page titles. */
const READ_KEYS = [
  'buttonText',
  'displayDetails',
  'resetButtonTitle',
  'backButtonTitle',
  'noResultsText',
  'recentTitle',
  'suggestedTitle',
  'selectText',
  'selectKeyAriaLabel',
  'navigateText',
  'navigateUpKeyAriaLabel',
  'navigateDownKeyAriaLabel',
  'closeText',
  'closeKeyAriaLabel',
  'closeKeyLabel',
] as const

describe('search modal texts', () => {
  it('reads the language of the open page through useUi', () => {
    expect(source).toContain("import { useUi } from '../useUi'")
    expect(source).toContain('useUi()')
  })

  it('has no English defaults, no fallback translator and no bare key label', () => {
    for (const english of ['Recently viewed', 'Suggested', 'Reset search', 'Close search', 'No results for', 'to navigate', 'to select', 'to close']) {
      expect(source).not.toContain(`'${english}'`)
    }
    expect(source).not.toContain('createSearchTranslate')
    expect(source).not.toContain('>esc</kbd>')
    expect(source).not.toMatch(/\|\|\s*'Search'/)
  })

  it('reads every text it shows from the search texts', () => {
    for (const key of READ_KEYS) {
      expect(source).toMatch(new RegExp(`searchText(\\.value)?\\.${key}\\b`))
    }
    expect(source).toContain('searchText.value.suggestedPages')
  })

  it.each(LOCALE_KEYS.map((locale) => [locale]))('%s has every key the component reads', (locale) => {
    const search = UI_STRINGS[locale].search as Record<string, unknown>
    for (const key of READ_KEYS) {
      expect(typeof search[key]).toBe('string')
      expect((search[key] as string).trim()).not.toBe('')
    }
  })
})
