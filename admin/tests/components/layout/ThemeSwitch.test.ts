/**
 * Mount tests for ThemeSwitch: the three icon buttons as one radio group, the stored choice marked, a click or an
 * arrow key applied and remembered.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import ThemeSwitch from '@/components/layout/ThemeSwitch.vue'
import { stubRoutes } from '../../helpers/routes'
import { mountScreen } from '../../helpers/mount'
import { resetThemeDocument, stubSchemeQuery } from '../../helpers/theme'
import { THEME_STORAGE_KEY } from '@/lib/themeBoot'
import { AdminTheme } from '@/types/admintheme'

/** Mounts the switch on the default (system) choice. */
const mountSwitch = async () => {
  resetThemeDocument()
  stubSchemeQuery(false)
  stubRoutes({})
  return (await mountScreen(ThemeSwitch, { testid: 'probe-theme' }, '/dashboard')).wrapper
}

describe('ThemeSwitch', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is a labelled radio group of Light, Dark and System with the stored choice checked', async () => {
    const wrapper = await mountSwitch()

    expect(wrapper.get('[data-testid="probe-theme"]').attributes('role')).toBe('radiogroup')
    expect(wrapper.findAll('[role="radio"]').map(button => button.attributes('aria-label'))).toEqual(['Light', 'Dark', 'System'])
    expect(wrapper.get('[data-testid="probe-theme-system"]').attributes('aria-checked')).toBe('true')
    expect(wrapper.get('[data-testid="probe-theme-dark"]').attributes('aria-checked')).toBe('false')
  })

  it('applies and remembers a clicked choice', async () => {
    const wrapper = await mountSwitch()

    await wrapper.get('[data-testid="probe-theme-dark"]').trigger('click')

    expect(wrapper.get('[data-testid="probe-theme-dark"]').attributes('aria-checked')).toBe('true')
    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe(AdminTheme.Dark)
    expect(document.documentElement.classList.contains('light')).toBe(false)
  })

  it('moves the choice with the arrow keys, wrapping around', async () => {
    const wrapper = await mountSwitch()

    await wrapper.get('[data-testid="probe-theme-system"]').trigger('keydown', { key: 'ArrowRight' })
    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe(AdminTheme.Light)

    await wrapper.get('[data-testid="probe-theme-light"]').trigger('keydown', { key: 'ArrowLeft' })
    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe(AdminTheme.System)
  })
})
