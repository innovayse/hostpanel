/**
 * Stand-ins for the Nuxt route auto-imports that every page-level test helper needs.
 *
 * The client, public-page and auth helpers each stood in for `useRoute` with the same object
 * literal; this module is the one definition they share, and the locale prefix the localised
 * path stand-in uses, so a test that expects a localised link names it once.
 *
 * @module tests/helpers/runtimeStubs
 */

import { vi } from 'vitest'
import { ref } from 'vue'

/** What a test can change about the stood-in route. */
export interface RouteStubOptions {
  /** The route parameters (`route.params`). */
  params?: Record<string, string>
  /** The route query (`route.query`). */
  query?: Record<string, string | string[]>
  /** The path (`route.path`), which the SEO composable builds canonical URLs from; the root unless given. */
  path?: string
  /** The full path (`route.fullPath`). */
  fullPath?: string
}

/**
 * Defines `useRoute` as a global returning a fixed route.
 *
 * @param options - Parameters, query, path and full path of the route.
 */
export const stubRoute = (options: RouteStubOptions = {}): void => {
  vi.stubGlobal('useRoute', () => ({
    params: options.params ?? {},
    query: options.query ?? {},
    path: options.path ?? '/',
    fullPath: options.fullPath ?? '/',
    meta: {},
  }))
}

/**
 * Defines `useLocalePath` as a global that prefixes every path.
 *
 * @param prefix - What to put before the path; empty keeps the path as given.
 */
export const stubLocalePath = (prefix: string = ''): void => {
  vi.stubGlobal('useLocalePath', () => (path: string) => `${prefix}${path}`)
}

/**
 * Defines `useI18n` as a global that reports one active language, for a composable that reads only the locale.
 *
 * @param locale - The visitor's language code.
 */
export const stubLocale = (locale: string): void => {
  vi.stubGlobal('useI18n', () => ({ locale: ref(locale) }))
}

/** The locale prefix a test's localised path carries, for the tests that expect what `useLocalePath` returns. */
export const TEST_LOCALE_PREFIX = '/en'

/**
 * A path as the localised router returns it in a test.
 *
 * @param path - The unlocalised path, e.g. a client route constant or the result of a route helper.
 * @returns The path behind {@link TEST_LOCALE_PREFIX}.
 */
export const localized = (path: string): string => `${TEST_LOCALE_PREFIX}${path}`

/**
 * Defines `useSwitchLocalePath` as a global that turns a language code into that language's path, as the i18n router
 * does for the language switcher.
 */
export const stubSwitchLocalePath = (): void => {
  vi.stubGlobal('useSwitchLocalePath', () => (code: string) => `/${code}`)
}
