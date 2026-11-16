/**
 * Tests for `useAppColorMode`: the visitor's cookie wins over the operator's default, `system`
 * leaves the mode unresolved, and the toggle respects the operator's switch.
 *
 * @module tests/composables/useAppColorMode.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ref } from 'vue'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { SETTING_OFF } from '~/constants/settingValue'
import { ColorMode } from '~/types/colormode'
import { ColorModeSystem } from '~/types/colormodesystem'
import { COLOR_MODE_COOKIE } from '~/utils/colorMode'
import { useAppColorMode } from '~/composables/useAppColorMode'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer()

/** The cookie ref `useCookie` hands back, replaced before each test. */
const cookie = ref<string | null>(null)

/** Spy for `useCookie`, to read the name and options the composable asked for. */
const useCookie = vi.fn(() => cookie)

describe('useAppColorMode', () => {
  beforeEach(() => {
    cookie.value = null
    useCookie.mockClear()
    vi.stubGlobal('useCookie', useCookie)
  })

  it('reads the colour-mode cookie by its name', async () => {
    await loadPortalSettings(replies)

    useAppColorMode()

    expect(useCookie).toHaveBeenCalledWith(COLOR_MODE_COOKIE, expect.objectContaining({ sameSite: 'lax' }))
  })

  it('falls back to dark when the operator set nothing and the visitor chose nothing', async () => {
    await loadPortalSettings(replies)

    const { isDark, mode, applied } = useAppColorMode()

    expect(applied.value).toBe(ColorMode.Dark)
    expect(mode.value).toBe(ColorMode.Dark)
    expect(isDark.value).toBe(true)
  })

  it('applies the operator default when it is light', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ThemeDefault]: ColorMode.Light })

    const { isDark, applied } = useAppColorMode()

    expect(applied.value).toBe(ColorMode.Light)
    expect(isDark.value).toBe(false)
  })

  it('leaves the applied mode unresolved under system and renders it as dark', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ThemeDefault]: ColorModeSystem.System })

    const { applied, mode, isDark } = useAppColorMode()

    expect(applied.value).toBeNull()
    expect(mode.value).toBe(ColorModeSystem.System)
    expect(isDark.value).toBe(true)
  })

  it('lets the visitor cookie win over the operator default', async () => {
    cookie.value = ColorMode.Light
    await loadPortalSettings(replies, { [PortalSettingKey.ThemeDefault]: ColorMode.Dark })

    const { applied, mode, isDark } = useAppColorMode()

    expect(applied.value).toBe(ColorMode.Light)
    expect(mode.value).toBe(ColorMode.Light)
    expect(isDark.value).toBe(false)
  })

  it('reports the toggle as enabled unless the operator turned it off', async () => {
    await loadPortalSettings(replies)
    expect(useAppColorMode().userToggleEnabled.value).toBe(true)

    await loadPortalSettings(replies, { [PortalSettingKey.ThemeUserToggle]: SETTING_OFF })
    expect(useAppColorMode().userToggleEnabled.value).toBe(false)
  })

  it('choose applies the mode and remembers it in the cookie', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ThemeDefault]: ColorModeSystem.System })
    const { choose, applied, mode } = useAppColorMode()

    choose(ColorMode.Light)

    expect(applied.value).toBe(ColorMode.Light)
    expect(mode.value).toBe(ColorMode.Light)
    expect(cookie.value).toBe(ColorMode.Light)
  })

  it('toggle flips dark to light and back, writing the cookie each time', async () => {
    await loadPortalSettings(replies)
    const { toggle, isDark } = useAppColorMode()

    toggle()
    expect(isDark.value).toBe(false)
    expect(cookie.value).toBe(ColorMode.Light)

    toggle()
    expect(isDark.value).toBe(true)
    expect(cookie.value).toBe(ColorMode.Dark)
  })

  it('toggle does nothing when the operator disabled the switch', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ThemeUserToggle]: SETTING_OFF })
    const { toggle, isDark } = useAppColorMode()

    toggle()

    expect(isDark.value).toBe(true)
    expect(cookie.value).toBeNull()
  })
})
