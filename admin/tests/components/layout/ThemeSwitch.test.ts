/**
 * Mount tests for ThemeSwitch: the three icon buttons as one radio group, the stored choice marked, a click or an
 * arrow key applied and remembered.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import ThemeSwitch from '@/components/layout/ThemeSwitch.vue'
import { createPinia, setActivePinia } from 'pinia'
import { mount } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import { createTestI18n } from '../../helpers/i18n'
import { resetThemeDocument, stubSchemeQuery } from '../../helpers/theme'
import { THEME_STORAGE_KEY } from '@/lib/themeBoot'
import { AdminTheme } from '@/types/admintheme'

/** Wrappers mounted by the current test, unmounted after it so attached DOM and focus do not leak. */
const mounted: VueWrapper[] = []

/**
 * Mounts the switch on the default (system) choice, attached to the page so focus can be read.
 *
 * @returns The wrapper.
 */
const mountSwitch = () => {
  resetThemeDocument()
  stubSchemeQuery(false)
  const pinia = createPinia()
  setActivePinia(pinia)
  const wrapper = mount(ThemeSwitch, {
    props: { testid: 'probe-theme' },
    attachTo: document.body,
    global: { plugins: [createTestI18n(), pinia] },
  })
  mounted.push(wrapper)
  return wrapper
}

describe('ThemeSwitch', () => {
  afterEach(() => {
    mounted.splice(0).forEach(wrapper => wrapper.unmount())
    vi.unstubAllGlobals()
  })

  it('is a labelled radio group of Light, Dark and System with the stored choice checked', () => {
    const wrapper = mountSwitch()

    expect(wrapper.get('[data-testid="probe-theme"]').attributes('role')).toBe('radiogroup')
    expect(wrapper.findAll('[role="radio"]').map(button => button.attributes('aria-label'))).toEqual(['Light', 'Dark', 'System'])
    expect(wrapper.get('[data-testid="probe-theme-system"]').attributes('aria-checked')).toBe('true')
    expect(wrapper.get('[data-testid="probe-theme-dark"]').attributes('aria-checked')).toBe('false')
  })

  it('applies and remembers a clicked choice', async () => {
    const wrapper = mountSwitch()

    await wrapper.get('[data-testid="probe-theme-dark"]').trigger('click')

    expect(wrapper.get('[data-testid="probe-theme-dark"]').attributes('aria-checked')).toBe('true')
    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe(AdminTheme.Dark)
    expect(document.documentElement.classList.contains('light')).toBe(false)
  })

  it('moves the choice with the arrow keys, wrapping around', async () => {
    const wrapper = mountSwitch()

    await wrapper.get('[data-testid="probe-theme-system"]').trigger('keydown', { key: 'ArrowRight' })
    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe(AdminTheme.Light)

    await wrapper.get('[data-testid="probe-theme-light"]').trigger('keydown', { key: 'ArrowLeft' })
    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe(AdminTheme.System)
  })

  it('moves focus with the choice, so a second arrow press goes on from the new button', async () => {
    const wrapper = mountSwitch()
    const system = wrapper.get('[data-testid="probe-theme-system"]')
    ;(system.element as HTMLElement).focus()

    await system.trigger('keydown', { key: 'ArrowRight' })
    expect(document.activeElement).toBe(wrapper.get('[data-testid="probe-theme-light"]').element)
    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe(AdminTheme.Light)

    await wrapper.get('[data-testid="probe-theme-light"]').trigger('keydown', { key: 'ArrowRight' })
    expect(document.activeElement).toBe(wrapper.get('[data-testid="probe-theme-dark"]').element)
    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe(AdminTheme.Dark)
  })

  it('jumps to the first and last choice with Home and End', async () => {
    const wrapper = mountSwitch()

    await wrapper.get('[data-testid="probe-theme-system"]').trigger('keydown', { key: 'Home' })
    expect(document.activeElement).toBe(wrapper.get('[data-testid="probe-theme-light"]').element)

    await wrapper.get('[data-testid="probe-theme-light"]').trigger('keydown', { key: 'End' })
    expect(document.activeElement).toBe(wrapper.get('[data-testid="probe-theme-system"]').element)
    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe(AdminTheme.System)
  })

  it('moves the choice and focus with Down and Up, wrapping around', async () => {
    const wrapper = mountSwitch()
    const system = wrapper.get('[data-testid="probe-theme-system"]')
    ;(system.element as HTMLElement).focus()

    await system.trigger('keydown', { key: 'ArrowDown' })
    expect(document.activeElement).toBe(wrapper.get('[data-testid="probe-theme-light"]').element)
    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe(AdminTheme.Light)

    await wrapper.get('[data-testid="probe-theme-light"]').trigger('keydown', { key: 'ArrowUp' })
    expect(document.activeElement).toBe(wrapper.get('[data-testid="probe-theme-system"]').element)
    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe(AdminTheme.System)
  })

  it('draws an icon inside every theme button', () => {
    const wrapper = mountSwitch()

    for (const theme of [AdminTheme.Light, AdminTheme.Dark, AdminTheme.System]) {
      expect(wrapper.get(`[data-testid="probe-theme-${theme}"] svg path`).attributes('d')).toBeTruthy()
    }
  })
})
