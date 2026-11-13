/**
 * Mount tests for ThemeMenu: the icon trigger and its accessible name, the three options with the current one
 * checked, and a pick applied and remembered.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import ThemeMenu from '@/components/layout/ThemeMenu.vue'
import { stubRoutes } from '../../helpers/routes'
import { mountScreen } from '../../helpers/mount'
import { resetThemeDocument, stubSchemeQuery } from '../../helpers/theme'
import { THEME_STORAGE_KEY } from '@/lib/themeBoot'
import { AdminTheme } from '@/types/admintheme'

describe('ThemeMenu', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('names the trigger and lists the three themes with the current one checked', async () => {
    resetThemeDocument()
    stubSchemeQuery(false)
    stubRoutes({})
    const { wrapper } = await mountScreen(ThemeMenu, {}, '/dashboard')
    const trigger = wrapper.get('[data-testid="theme-menu"]')

    expect(trigger.attributes('aria-label')).toBe('Theme')
    expect(trigger.attributes('aria-haspopup')).toBe('menu')
    await trigger.trigger('click')

    expect(wrapper.findAll('[role="menuitemradio"]').map(item => item.text())).toEqual(['Light', 'Dark', 'System'])
    expect(wrapper.get('[data-testid="theme-option-system"]').attributes('aria-checked')).toBe('true')
    expect(wrapper.get('[data-testid="theme-option-dark"]').attributes('aria-checked')).toBe('false')
  })

  it('applies and remembers the picked theme', async () => {
    resetThemeDocument()
    stubSchemeQuery(false)
    stubRoutes({})
    const { wrapper } = await mountScreen(ThemeMenu, { testid: 'probe-theme' }, '/dashboard')

    await wrapper.get('[data-testid="probe-theme"]').trigger('click')
    await wrapper.get('[data-testid="theme-option-dark"]').trigger('click')

    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe(AdminTheme.Dark)
    expect(document.documentElement.classList.contains('light')).toBe(false)
    await wrapper.get('[data-testid="probe-theme"]').trigger('click')
    expect(wrapper.get('[data-testid="theme-option-dark"]').attributes('aria-checked')).toBe('true')
  })
})
