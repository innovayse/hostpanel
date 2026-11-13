/**
 * Helpers for theme tests: a scriptable `prefers-color-scheme` query and a clean document.
 */

import { vi } from 'vitest'

/** A fake system scheme query whose answer a test can flip. */
export interface FakeSchemeQuery {
  /** Sets whether the system prefers light and notifies the listeners. */
  setLight: (light: boolean) => void
}

/**
 * Stubs `window.matchMedia` with a query the test controls.
 *
 * @param light - Whether the system prefers light at first.
 * @returns The controller.
 */
export const stubSchemeQuery = (light: boolean): FakeSchemeQuery => {
  /** Listeners added through `addEventListener('change', listener)`. */
  const listeners: Array<() => void> = []
  /** The live query object; `matches` is read on every call. */
  const query = {
    matches: light,
    addEventListener: (_type: string, listener: () => void): void => {
      listeners.push(listener)
    },
  }
  vi.stubGlobal('matchMedia', () => query)
  return {
    setLight: (next: boolean): void => {
      query.matches = next
      listeners.forEach((listener) => {
        listener()
      })
    },
  }
}

/** Resets `<html>`, the `theme-color` metas and storage to a fresh page. */
export const resetThemeDocument = (): void => {
  document.documentElement.className = ''
  document.head.innerHTML =
    '<meta name="theme-color" media="(prefers-color-scheme: light)" content="#f5f7fc">'
    + '<meta name="theme-color" media="(prefers-color-scheme: dark)" content="#070b18">'
  window.localStorage.clear()
}

/**
 * The content of every `theme-color` meta.
 *
 * @returns One entry per meta.
 */
export const themeColors = (): Array<string | null> =>
  Array.from(document.querySelectorAll('meta[name="theme-color"]')).map((meta) => meta.getAttribute('content'))
