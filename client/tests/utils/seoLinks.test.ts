/**
 * Tests for `utils/seoLinks.ts`: the default language has no URL prefix, every other one does, and
 * every page gets a canonical link, an alternate per language and an `x-default`.
 *
 * @module tests/utils/seoLinks.test
 */

import { describe, expect, it } from 'vitest'
import { AppLocale } from '~/types/applocale'
import { HREFLANG_DEFAULT, buildSeoLinks, localizedUrl, stripLocalePrefix } from '~/utils/seoLinks'

/** Site origin used by every case. */
const BASE = 'https://portal.test'

describe('stripLocalePrefix', () => {
  it.each([
    ['/ru/hosting', '/hosting'],
    ['/hy', ''],
    ['/de/hosting', '/hosting'],
    ['/es/hosting', '/hosting'],
    ['/hosting', '/hosting'],
  ])('turns %s into %s', (path, expected) => {
    expect(stripLocalePrefix(path)).toBe(expected)
  })
})

describe('localizedUrl', () => {
  it('leaves the default language unprefixed and falls back to the root', () => {
    expect(localizedUrl(BASE, '/hosting', AppLocale.En)).toBe(`${BASE}/hosting`)
    expect(localizedUrl(BASE, '', AppLocale.En)).toBe(`${BASE}/`)
  })

  it('prefixes every other language', () => {
    expect(localizedUrl(BASE, '/hosting', AppLocale.Ru)).toBe(`${BASE}/ru/hosting`)
    expect(localizedUrl(BASE, '', AppLocale.Hy)).toBe(`${BASE}/hy`)
    expect(localizedUrl(BASE, '/hosting', AppLocale.De)).toBe(`${BASE}/de/hosting`)
    expect(localizedUrl(BASE, '/hosting', AppLocale.Es)).toBe(`${BASE}/es/hosting`)
  })
})

describe('buildSeoLinks', () => {
  it('builds the canonical for the served language and an alternate per language', () => {
    const links = buildSeoLinks(BASE, '/hosting', AppLocale.Ru)

    expect(links[0]).toEqual({ rel: 'canonical', href: `${BASE}/ru/hosting` })
    expect(links.flatMap((link) => (link.rel === 'alternate' ? [link.hreflang] : [])))
      .toEqual([AppLocale.En, AppLocale.Ru, AppLocale.Hy, AppLocale.De, AppLocale.Es, AppLocale.Fr, HREFLANG_DEFAULT])
  })

  it('points x-default at the default language', () => {
    const links = buildSeoLinks(BASE, '/hosting', AppLocale.Hy)

    expect(links.find((link) => link.rel === 'alternate' && link.hreflang === HREFLANG_DEFAULT)?.href).toBe(`${BASE}/hosting`)
  })
})
