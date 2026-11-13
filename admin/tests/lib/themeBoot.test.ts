/**
 * Unit tests for the theme pre-paint logic: the stored choice, the system fallback, unreadable
 * storage, the `theme-color` metas, and that the serialised inline script behaves the same.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { bootTheme, THEME_BOOT_CONFIG, THEME_STORAGE_KEY, themeBootScript } from '@/lib/themeBoot'
import { AdminTheme } from '@/types/admintheme'
import { resetThemeDocument, stubSchemeQuery, themeColors } from '../helpers/theme'

describe('bootTheme', () => {
  beforeEach(() => {
    resetThemeDocument()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('applies a stored light choice even when the system prefers dark', () => {
    stubSchemeQuery(false)
    window.localStorage.setItem(THEME_STORAGE_KEY, AdminTheme.Light)

    expect(bootTheme(THEME_BOOT_CONFIG)).toBe(true)
    expect(document.documentElement.classList.contains('light')).toBe(true)
    expect(themeColors()).toEqual(['#f5f7fc', '#f5f7fc'])
    expect(document.querySelector('meta[media]')).toBeNull()
  })

  it('applies a stored dark choice even when the system prefers light', () => {
    stubSchemeQuery(true)
    document.documentElement.classList.add('light')
    window.localStorage.setItem(THEME_STORAGE_KEY, AdminTheme.Dark)

    expect(bootTheme(THEME_BOOT_CONFIG)).toBe(false)
    expect(document.documentElement.classList.contains('light')).toBe(false)
    expect(themeColors()).toEqual(['#070b18', '#070b18'])
  })

  it('follows the system for "system", no choice or an unknown value', () => {
    stubSchemeQuery(true)
    expect(bootTheme(THEME_BOOT_CONFIG)).toBe(true)

    window.localStorage.setItem(THEME_STORAGE_KEY, AdminTheme.System)
    expect(bootTheme(THEME_BOOT_CONFIG)).toBe(true)

    window.localStorage.setItem(THEME_STORAGE_KEY, 'sepia')
    expect(bootTheme(THEME_BOOT_CONFIG)).toBe(true)
  })

  it('falls back to dark without matchMedia', () => {
    vi.stubGlobal('matchMedia', undefined)

    expect(bootTheme(THEME_BOOT_CONFIG)).toBe(false)
  })

  it('treats storage that throws as "follow the system"', () => {
    stubSchemeQuery(true)
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked')
    })

    expect(bootTheme(THEME_BOOT_CONFIG)).toBe(true)
  })
})

describe('themeBootScript', () => {
  beforeEach(() => {
    resetThemeDocument()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('runs standalone, with no imports, and applies the stored choice', () => {
    stubSchemeQuery(false)
    window.localStorage.setItem(THEME_STORAGE_KEY, AdminTheme.Light)

    // Evaluated as the browser would evaluate the inline <script>: plain source, no module scope.
    new Function(themeBootScript())()

    expect(document.documentElement.classList.contains('light')).toBe(true)
    expect(themeColors()).toEqual(['#f5f7fc', '#f5f7fc'])
  })

  it('embeds the panel config', () => {
    expect(themeBootScript()).toContain(JSON.stringify(THEME_BOOT_CONFIG))
  })
})
