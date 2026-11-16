/**
 * Tests for `tests/helpers/runtimeStubs.ts`: the one stand-in for the route auto-imports.
 *
 * @module tests/helpers/runtimeStubs.test
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { localized, stubLocale, stubLocalePath, stubRoute, stubSwitchLocalePath, TEST_LOCALE_PREFIX } from '~/tests/helpers/runtimeStubs'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('stubRoute', () => {
  it('answers with the given parameters, query and path', () => {
    stubRoute({ params: { id: '7' }, query: { tab: 'x' }, fullPath: '/a?tab=x' })

    const route = useRoute()

    expect(route.params).toEqual({ id: '7' })
    expect(route.query).toEqual({ tab: 'x' })
    expect(route.fullPath).toBe('/a?tab=x')
  })

  it('answers with the path it was given', () => {
    stubRoute({ path: '/announcements/11' })

    expect(useRoute().path).toBe('/announcements/11')
  })

  it('defaults to an empty route at the root', () => {
    stubRoute()

    expect(useRoute().fullPath).toBe('/')
    expect(useRoute().path).toBe('/')
  })
})

describe('stubLocalePath', () => {
  it('keeps the path without a prefix and adds one when asked', () => {
    stubLocalePath()
    expect(useLocalePath()('/hosting')).toBe('/hosting')

    stubLocalePath('/en')
    expect(useLocalePath()('/hosting')).toBe('/en/hosting')
  })
})

describe('stubLocale', () => {
  it('reports the language it was given as the active one', () => {
    stubLocale('ru')

    expect(useI18n().locale.value).toBe('ru')
  })
})

describe('TEST_LOCALE_PREFIX and localized', () => {
  it('puts the test locale prefix before a path', () => {
    expect(TEST_LOCALE_PREFIX).toBe('/en')
    expect(localized('/hosting')).toBe('/en/hosting')
  })

  it('matches what the localised path stand-in returns for the same prefix', () => {
    stubLocalePath(TEST_LOCALE_PREFIX)

    expect(useLocalePath()('/hosting')).toBe(localized('/hosting'))
  })
})

describe('stubSwitchLocalePath', () => {
  it('turns a language code into that language path', () => {
    stubSwitchLocalePath()

    expect(useSwitchLocalePath()('ru')).toBe('/ru')
  })
})
