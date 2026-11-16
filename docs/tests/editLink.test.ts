/**
 * Tests of the "Edit this page" link and of the one rule behind its shape: the site data VitePress writes into every
 * page must hold no function. A function is written as source text and rebuilt in the browser with `new Function`,
 * which a Content-Security-Policy without `'unsafe-eval'` refuses, so the whole documentation app would fail.
 */
import { describe, expect, it } from 'vitest'
import config from '../.vitepress/config'
import { EDIT_LINK_PATTERN, repositoryPathOf } from '../.vitepress/editLink'
import { FALLBACK_DIR } from '../.vitepress/i18n/locales'

/**
 * Collects the place of every function in a value that VitePress serializes.
 * @param value The value to walk.
 * @param path Where the value sits, for the message.
 * @returns The places that hold a function; empty when the value is plain data.
 */
const functionsIn = (value: unknown, path: string): string[] => {
  if (typeof value === 'function') {
    return [path]
  }
  if (typeof value === 'object' && value !== null) {
    return Object.entries(value).flatMap(([key, child]) => functionsIn(child, `${path}.${key}`))
  }
  return []
}

describe('the edit link', () => {
  it('is a plain pattern whose :path is the source path of the page', () => {
    expect(EDIT_LINK_PATTERN).toBe('https://github.com/innovayse/hostpanel/edit/main/docs/:path')
  })

  it('keeps the real source path of a page', () => {
    expect(repositoryPathOf('en/staff/orders.md')).toBe('en/staff/orders.md')
    expect(repositoryPathOf('ru/staff/orders.md')).toBe('ru/staff/orders.md')
  })

  it('sends a placeholder page to the path where its translation is created', () => {
    expect(repositoryPathOf(`${FALLBACK_DIR}/ru/staff/orders.md`)).toBe('ru/staff/orders.md')
  })

  it('does not touch a path that only contains the placeholder folder name further in', () => {
    expect(repositoryPathOf(`en/${FALLBACK_DIR}/x.md`)).toBe(`en/${FALLBACK_DIR}/x.md`)
  })
})

describe('the site data of the real config', () => {
  it('holds no function, so no page needs unsafe-eval to read it', () => {
    expect(functionsIn(config.themeConfig, 'themeConfig')).toEqual([])
    expect(functionsIn(config.locales, 'locales')).toEqual([])
  })

  it('gives every language the same edit link pattern', () => {
    const locales = config.locales ?? {}
    const patterns = Object.entries(locales)
      .filter(([key]) => key !== 'root')
      .map(([, locale]) => (locale.themeConfig as { editLink: { pattern: string } }).editLink.pattern)
    expect(patterns.length).toBeGreaterThan(0)
    expect(new Set(patterns)).toEqual(new Set([EDIT_LINK_PATTERN]))
  })
})
