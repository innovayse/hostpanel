/**
 * Mount tests for ThemeToggle: the button's accessible name and pressed state, the icon of the current theme, and a click
 * that switches straight to the other theme and remembers it.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import ThemeToggle from '@/components/layout/ThemeToggle.vue'
import { stubRoutes } from '../../helpers/routes'
import { mountScreen } from '../../helpers/mount'
import { resetThemeDocument, stubSchemeQuery } from '../../helpers/theme'
import { Icon } from '@/lib/icons'
import { THEME_STORAGE_KEY } from '@/lib/themeBoot'
import { AdminTheme } from '@/types/admintheme'

/**
 * Mounts the toggle on a page whose system scheme is the given one.
 *
 * @param systemLight - Whether the operating system prefers light.
 * @returns The mounted toggle.
 */
const mountToggle = async (systemLight: boolean) => {
  resetThemeDocument()
  stubSchemeQuery(systemLight)
  stubRoutes({})
  const { wrapper } = await mountScreen(ThemeToggle, {}, '/dashboard')
  return wrapper
}

/**
 * Outline of an icon: the `d` of its path, which is what the rendered svg carries whatever way its tag is written out.
 *
 * @param markup - Stored markup of one path icon.
 * @returns The path data.
 */
const outlineOf = (markup: string): string | undefined => /\sd="([^"]+)"/.exec(markup)?.[1]

describe('ThemeToggle', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is a plain button with a name and a pressed state, not a menu', async () => {
    const wrapper = await mountToggle(false)
    const button = wrapper.get('[data-testid="theme-toggle"]')

    expect(button.element.tagName).toBe('BUTTON')
    expect(button.attributes('aria-label')).toBe('Dark theme')
    expect(button.attributes('aria-pressed')).toBe('true')
    expect(button.attributes('aria-haspopup')).toBeUndefined()
    expect(wrapper.find('[role="menu"]').exists()).toBe(false)
  })

  it('shows the moon and is pressed in dark, the sun and not pressed in light', async () => {
    const dark = await mountToggle(false)
    expect(dark.get('[data-testid="theme-toggle"] path').attributes('d')).toBe(outlineOf(Icon.Moon))

    const light = await mountToggle(true)
    const button = light.get('[data-testid="theme-toggle"]')
    expect(button.get('path').attributes('d')).toBe(outlineOf(Icon.Sun))
    expect(button.attributes('aria-pressed')).toBe('false')
  })

  it('switches to the other theme on one click, remembers it and switches back', async () => {
    const wrapper = await mountToggle(false)
    const button = wrapper.get('[data-testid="theme-toggle"]')

    await button.trigger('click')

    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe(AdminTheme.Light)
    expect(document.documentElement.classList.contains('light')).toBe(true)
    expect(button.attributes('aria-pressed')).toBe('false')
    expect(button.get('path').attributes('d')).toBe(outlineOf(Icon.Sun))

    await button.trigger('click')

    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe(AdminTheme.Dark)
    expect(document.documentElement.classList.contains('light')).toBe(false)
    expect(button.attributes('aria-pressed')).toBe('true')
  })
})
