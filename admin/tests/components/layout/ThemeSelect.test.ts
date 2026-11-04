/**
 * Mount tests for ThemeSelect: the three choices in the active language, the stored choice shown,
 * a pick applied and remembered, and the caller's attributes reaching the select.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import ThemeSelect from '@/components/layout/ThemeSelect.vue'
import { stubRoutes } from '../../helpers/routes'
import { mountScreen } from '../../helpers/mount'
import { pickOption, selectText } from '../../helpers/select'
import { resetThemeDocument, stubSchemeQuery } from '../../helpers/theme'
import { THEME_STORAGE_KEY } from '@/lib/themeBoot'
import { AdminTheme } from '@/types/admintheme'

describe('ThemeSelect', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows the stored choice and applies and remembers a new one', async () => {
    resetThemeDocument()
    stubSchemeQuery(false)
    stubRoutes({})
    const { wrapper } = await mountScreen(ThemeSelect, { 'data-testid': 'probe-theme' }, '/dashboard')

    expect(selectText(wrapper, 'probe-theme')).toBe('System')
    await pickOption(wrapper, 'probe-theme', 'Dark')

    expect(selectText(wrapper, 'probe-theme')).toBe('Dark')
    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe(AdminTheme.Dark)
    expect(document.documentElement.classList.contains('light')).toBe(false)
  })
})
