/**
 * Tests for `useAppColorMode`: the visitor's cookie wins over the operator's default, `system`
 * leaves the mode unresolved, and the toggle respects the operator's switch.
 *
 * @module tests/composables/useAppColorMode.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ref } from 'vue'
import type { Ref } from 'vue'
import { SETTING_OFF } from '~/constants/settingValue'
import { ColorMode } from '~/types/colormode'
import { COLOR_MODE_COOKIE } from '~/utils/colorMode'
import { useAppColorMode } from '~/composables/useAppColorMode'

/** The cookie ref `useCookie` hands back, replaced before each test. */
const cookie = ref<string | null>(null)

/** Spy for `useCookie`, to read the name and options the composable asked for. */
const useCookie = vi.fn(() => cookie)

/**
 * Stands in for the Nuxt globals the composable reads.
 *
 * `useState` keeps one ref per key for the whole test, as Nuxt does per request.
 *
 * @param settings Operator setting key to value.
 */
const stubRuntime = (settings: Record<string, string> = {}): void => {
  const state = new Map<string, Ref<unknown>>()
  vi.stubGlobal('useCookie', useCookie)
  vi.stubGlobal('useState', (key: string, init: () => unknown) => {
    if (!state.has(key)) {
      state.set(key, ref(init()))
    }
    return state.get(key)
  })
  vi.stubGlobal('usePortalSettings', () => ({
    get: (key: string, _legacy: string, fallback = '') => settings[key] ?? fallback,
  }))
}

describe('useAppColorMode', () => {
  beforeEach(() => {
    cookie.value = null
    useCookie.mockClear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('reads the colour-mode cookie by its name', () => {
    stubRuntime()

    useAppColorMode()

    expect(useCookie).toHaveBeenCalledWith(COLOR_MODE_COOKIE, expect.objectContaining({ sameSite: 'lax' }))
  })

  it('falls back to dark when the operator set nothing and the visitor chose nothing', () => {
    stubRuntime()

    const { isDark, mode, applied } = useAppColorMode()

    expect(applied.value).toBe(ColorMode.Dark)
    expect(mode.value).toBe(ColorMode.Dark)
    expect(isDark.value).toBe(true)
  })

  it('applies the operator default when it is light', () => {
    stubRuntime({ 'portal.theme.default': 'light' })

    const { isDark, applied } = useAppColorMode()

    expect(applied.value).toBe(ColorMode.Light)
    expect(isDark.value).toBe(false)
  })

  it('leaves the applied mode unresolved under system and renders it as dark', () => {
    stubRuntime({ 'portal.theme.default': 'system' })

    const { applied, mode, isDark } = useAppColorMode()

    expect(applied.value).toBeNull()
    expect(mode.value).toBe('system')
    expect(isDark.value).toBe(true)
  })

  it('lets the visitor cookie win over the operator default', () => {
    cookie.value = 'light'
    stubRuntime({ 'portal.theme.default': 'dark' })

    const { applied, mode, isDark } = useAppColorMode()

    expect(applied.value).toBe(ColorMode.Light)
    expect(mode.value).toBe(ColorMode.Light)
    expect(isDark.value).toBe(false)
  })

  it('reports the toggle as enabled unless the operator turned it off', () => {
    stubRuntime()
    expect(useAppColorMode().userToggleEnabled.value).toBe(true)

    stubRuntime({ 'portal.theme.user_toggle': SETTING_OFF })
    expect(useAppColorMode().userToggleEnabled.value).toBe(false)
  })

  it('choose applies the mode and remembers it in the cookie', () => {
    stubRuntime({ 'portal.theme.default': 'system' })
    const { choose, applied, mode } = useAppColorMode()

    choose(ColorMode.Light)

    expect(applied.value).toBe(ColorMode.Light)
    expect(mode.value).toBe(ColorMode.Light)
    expect(cookie.value).toBe('light')
  })

  it('toggle flips dark to light and back, writing the cookie each time', () => {
    stubRuntime()
    const { toggle, isDark } = useAppColorMode()

    toggle()
    expect(isDark.value).toBe(false)
    expect(cookie.value).toBe('light')

    toggle()
    expect(isDark.value).toBe(true)
    expect(cookie.value).toBe('dark')
  })

  it('toggle does nothing when the operator disabled the switch', () => {
    stubRuntime({ 'portal.theme.user_toggle': SETTING_OFF })
    const { toggle, isDark } = useAppColorMode()

    toggle()

    expect(isDark.value).toBe(true)
    expect(cookie.value).toBeNull()
  })
})
