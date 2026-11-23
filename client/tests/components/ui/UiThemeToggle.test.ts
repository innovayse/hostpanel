/**
 * Unit tests for ThemeToggle: hidden when the operator disallows switching, otherwise a button
 * that toggles the mode and names the mode it would switch to once mounted.
 */
import { describe, expect, it } from 'vitest'
import { nextTick } from 'vue'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import ThemeToggle from '~/components/ui/UiThemeToggle.vue'
import { useAppColorMode } from '~/composables/useAppColorMode'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { SETTING_OFF } from '~/constants/settingValue'
import { ColorMode } from '~/types/colormode'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer({ useAppColorMode })

/** The operator's settings for a light storefront the visitor may switch. */
const LIGHT_DEFAULT: Record<string, string> = { [PortalSettingKey.ThemeDefault]: ColorMode.Light }

describe('ThemeToggle', () => {
  it('renders nothing when the operator disallows switching', async () => {
    await loadPortalSettings(replies, { ...LIGHT_DEFAULT, [PortalSettingKey.ThemeUserToggle]: SETTING_OFF })

    const wrapper = mountWithKit(ThemeToggle)

    expect(wrapper.find('[data-testid="theme-toggle"]').exists()).toBe(false)
  })

  it('renders a button that toggles the mode', async () => {
    await loadPortalSettings(replies, LIGHT_DEFAULT)
    const wrapper = mountWithKit(ThemeToggle)
    expect(useAppColorMode().isDark.value).toBe(false)

    await wrapper.find('[data-testid="theme-toggle"]').trigger('click')

    expect(useAppColorMode().isDark.value).toBe(true)
  })

  it('offers dark mode while light, and light mode while dark, once mounted', async () => {
    await loadPortalSettings(replies, LIGHT_DEFAULT)
    const wrapper = mountWithKit(ThemeToggle)
    await nextTick()
    expect(wrapper.find('[data-testid="theme-toggle"]').attributes('aria-label')).toBe('ui.theme.switchDark')

    await wrapper.find('[data-testid="theme-toggle"]').trigger('click')

    expect(wrapper.find('[data-testid="theme-toggle"]').attributes('aria-label')).toBe('ui.theme.switchLight')
  })

  it('names the action from the first render when the server already knew the mode', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ThemeDefault]: ColorMode.Dark })

    const wrapper = mountWithKit(ThemeToggle)

    expect(wrapper.find('[data-testid="theme-toggle"]').attributes('aria-label')).toBe('ui.theme.switchLight')
  })

  it('draws both icons and lets the dark variant pick one', async () => {
    await loadPortalSettings(replies, LIGHT_DEFAULT)

    const wrapper = mountWithKit(ThemeToggle)

    expect(wrapper.findAll('svg')).toHaveLength(2)
  })

  it('shows the moon in dark mode and the sun in light mode, by class so SSR and hydration agree', async () => {
    await loadPortalSettings(replies, LIGHT_DEFAULT)

    const wrapper = mountWithKit(ThemeToggle)

    expect(wrapper.find('[data-testid="theme-toggle-moon"]').classes()).toEqual(expect.arrayContaining(['hidden', 'dark:block']))
    expect(wrapper.find('[data-testid="theme-toggle-sun"]').classes()).toEqual(expect.arrayContaining(['block', 'dark:hidden']))
  })

  it('takes the larger glyph and the screen frame when asked for the large bare button', async () => {
    await loadPortalSettings(replies, LIGHT_DEFAULT)

    const wrapper = mountWithKit(ThemeToggle, { props: { variant: 'bare', size: 'lg' }, attrs: { class: 'h-10 border-classic-border' } })

    const button = wrapper.find('[data-testid="theme-toggle"]')
    expect(button.classes()).toEqual(expect.arrayContaining(['w-11', 'h-11', 'sm:w-10', 'sm:h-10', 'border-classic-border']))
    expect(wrapper.find('[data-testid="theme-toggle-sun"]').attributes('width')).toBe('20')
  })
})
