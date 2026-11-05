/**
 * Unit tests for ClientLayoutTopBar: welcome text, the widget mount points, the local account
 * menu that replaces them where the platform widget is not served, and the mobile menu button.
 *
 * @module tests/components/client/layout/TopBar.test
 */

import { Home } from 'lucide-vue-next'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import TopBar from '~/components/client/layout/TopBar.vue'

/** The theme and language controls need runtime services this test does not provide. */
const STUBS = { UiThemeToggle: true, UiLanguageSwitcher: true, LogoLockup: true }

/**
 * Stands in for the runtime configuration.
 *
 * @param publicConfig - The public half of the configuration.
 */
const stubConfig = (publicConfig: Record<string, unknown>): void => {
  vi.stubGlobal('useRuntimeConfig', () => ({ public: publicConfig }))
}

/** The profile and links the local menu is built from. */
const MENU_PROPS = {
  fullName: 'Ada Lovelace',
  firstName: 'Ada',
  lastName: 'Lovelace',
  email: 'ada@example.com',
  navItems: [{ to: '/client/dashboard', label: 'Dashboard', icon: Home }],
  sidebarOpen: false
}

describe('ClientLayoutTopBar', () => {
  beforeEach(() => {
    stubConfig({ authMode: 'sso', mainUrl: 'https://main.example.test' })
  })

  it('greets the signed-in person by name', () => {
    const wrapper = mountClient(TopBar, { props: { fullName: 'Ada Lovelace', sidebarOpen: false }, stubs: STUBS })

    expect(wrapper.find('[data-testid="client-topbar-name"]').text()).toBe('Ada Lovelace')
  })

  it('hands the person to both widget mount points', () => {
    const user = JSON.stringify({ email: 'ada@example.com' })
    const wrapper = mountClient(TopBar, { props: { fullName: 'Ada', widgetUser: user, sidebarOpen: false }, stubs: STUBS })

    expect(wrapper.find('#inno-launcher-mount').attributes('data-user')).toBe(user)
    expect(wrapper.find('#inno-account-mount').attributes('data-user')).toBe(user)
  })

  it('leaves data-user off while the profile is loading', () => {
    const wrapper = mountClient(TopBar, { props: { fullName: '', sidebarOpen: false }, stubs: STUBS })

    expect(wrapper.find('#inno-account-mount').attributes('data-user')).toBeUndefined()
  })

  it('draws no local menu where the platform widget fills the mount', () => {
    const wrapper = mountClient(TopBar, { props: MENU_PROPS, stubs: STUBS })

    expect(wrapper.find('[data-testid="client-account-trigger"]').exists()).toBe(false)
  })

  it('emits toggleSidebar from the menu button', async () => {
    const wrapper = mountClient(TopBar, { props: { fullName: 'Ada', sidebarOpen: false }, stubs: STUBS })

    await wrapper.find('[data-testid="client-menu-toggle"]').trigger('click')

    expect(wrapper.emitted('toggleSidebar')).toHaveLength(1)
  })

  it('links the brand lockup to the home page', () => {
    const wrapper = mountClient(TopBar, { props: { fullName: 'Ada', sidebarOpen: false }, stubs: STUBS })
    const brand = wrapper.find('[data-testid="client-topbar-brand"]')

    expect(brand.attributes('href')).toBe('/')
    expect(brand.classes()).not.toContain('lg:hidden')
  })

  it('puts the welcome line on the right, after the brand and before the controls', () => {
    const wrapper = mountClient(TopBar, { props: { fullName: 'Ada Lovelace', sidebarOpen: false }, stubs: STUBS })
    const header = wrapper.get('[data-testid="client-topbar"]')
    const left = header.element.children[0] as HTMLElement
    const right = header.element.children[1] as HTMLElement

    expect(left.contains(wrapper.get('[data-testid="client-topbar-brand"]').element)).toBe(true)
    expect(left.querySelector('[data-testid="client-topbar-name"]')).toBeNull()
    expect(right.querySelector('[data-testid="client-topbar-name"]')?.textContent).toBe('Ada Lovelace')
  })
})

describe('ClientLayoutTopBar without the platform widget', () => {
  beforeEach(() => {
    stubConfig({ authMode: 'local', mainUrl: '' })
  })

  it('leaves out both widget mounts', () => {
    const wrapper = mountClient(TopBar, { props: MENU_PROPS, stubs: STUBS })

    expect(wrapper.find('#inno-account-mount').exists()).toBe(false)
    expect(wrapper.find('#inno-launcher-mount').exists()).toBe(false)
  })

  it('shows the initials in the trigger and the name, e-mail, links and Sign out once opened', async () => {
    const wrapper = mountClient(TopBar, { props: MENU_PROPS, stubs: STUBS })
    const trigger = wrapper.find('[data-testid="client-account-trigger"]')
    expect(trigger.text()).toBe('AL')
    expect(trigger.attributes('aria-label')).toBe('Ada Lovelace')

    await trigger.trigger('click')

    expect(wrapper.find('[data-testid="client-account-name"]').text()).toBe('Ada Lovelace')
    expect(wrapper.find('[data-testid="client-account-email"]').text()).toBe('ada@example.com')
    expect(wrapper.find('[data-testid="client-account-link-/client/dashboard"]').attributes('href')).toBe('/client/dashboard')
    expect(wrapper.find('[data-testid="client-account-sign-out"]').exists()).toBe(true)
  })

  it('falls back to the generic label while the profile loads', () => {
    const wrapper = mountClient(TopBar, { props: { fullName: '', navItems: [], sidebarOpen: false }, stubs: STUBS })

    expect(wrapper.find('[data-testid="client-account-trigger"]').attributes('aria-label')).toBe('nav.clientArea')
  })

  it('emits signOut and closes the panel when Sign out is chosen', async () => {
    const wrapper = mountClient(TopBar, { props: MENU_PROPS, stubs: STUBS })
    await wrapper.find('[data-testid="client-account-trigger"]').trigger('click')

    await wrapper.find('[data-testid="client-account-sign-out"]').trigger('click')

    expect(wrapper.emitted('signOut')).toHaveLength(1)
    expect(wrapper.find('[data-testid="ui-popover-panel"]').exists()).toBe(false)
  })
})
