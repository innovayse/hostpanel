/**
 * Unit tests for the theme store: following the system scheme until a choice is made, reading a stored choice, switching
 * between light and dark and persisting it, and tolerating storage that throws.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { THEME_STORAGE_KEY } from '@/lib/themeBoot'
import { useThemeStore } from '@/stores/theme'
import { AdminTheme } from '@/types/admintheme'
import { resetThemeDocument, stubSchemeQuery } from '../helpers/theme'

describe('theme store', () => {
  beforeEach(() => {
    resetThemeDocument()
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('follows the system with nothing stored', () => {
    stubSchemeQuery(true)
    const store = useThemeStore()

    store.init()

    expect(store.isLight).toBe(true)
    expect(document.documentElement.classList.contains('light')).toBe(true)
  })

  it('reads a stored choice', () => {
    stubSchemeQuery(true)
    window.localStorage.setItem(THEME_STORAGE_KEY, AdminTheme.Dark)
    const store = useThemeStore()

    store.init()

    expect(store.isLight).toBe(false)
  })

  it('toggles from the applied scheme, stores the choice and applies it', () => {
    stubSchemeQuery(false)
    const store = useThemeStore()
    store.init()

    store.toggle()

    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe(AdminTheme.Light)
    expect(store.isLight).toBe(true)
    expect(document.documentElement.classList.contains('light')).toBe(true)

    store.toggle()

    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe(AdminTheme.Dark)
    expect(store.isLight).toBe(false)
    expect(document.documentElement.classList.contains('light')).toBe(false)
  })

  it('follows a system scheme change until a choice is made, not after', () => {
    const scheme = stubSchemeQuery(false)
    const store = useThemeStore()
    store.init()

    scheme.setLight(true)
    expect(store.isLight).toBe(true)

    store.toggle()
    expect(store.isLight).toBe(false)
    scheme.setLight(true)
    expect(store.isLight).toBe(false)
  })

  it('adds the system listener only once', () => {
    const addEventListener = vi.fn()
    vi.stubGlobal('matchMedia', () => ({ matches: false, addEventListener }))
    const store = useThemeStore()

    store.init()
    store.init()

    expect(addEventListener).toHaveBeenCalledTimes(1)
  })

  it('still applies and keeps a choice for the page when storage throws', () => {
    stubSchemeQuery(false)
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    const store = useThemeStore()
    store.init()

    store.toggle()
    expect(store.isLight).toBe(true)

    store.toggle()
    expect(store.isLight).toBe(false)
  })
})
