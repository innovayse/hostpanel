/**
 * Unit tests for ClientLayoutAccountMenu: the initials trigger, the panel with the person, the
 * links and Sign out.
 *
 * @module tests/components/client/layout/AccountMenu.test
 */

import { Home, UserCog } from 'lucide-vue-next'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import AccountMenu from '~/components/client/layout/AccountMenu.vue'

afterEach(() => { vi.unstubAllGlobals() })

/** The person and links the menu is built from. */
const PROPS = {
  firstName: 'Ada',
  lastName: 'Lovelace',
  email: 'ada@example.com',
  items: [
    { to: '/client/dashboard', label: 'Dashboard', icon: Home },
    { to: '/client/account', label: 'Account', icon: UserCog }
  ]
}

describe('ClientLayoutAccountMenu', () => {
  it('shows only the initials until it is opened, named after the person', () => {
    const wrapper = mountClient(AccountMenu, { props: PROPS })
    const trigger = wrapper.find('[data-testid="client-account-trigger"]')

    expect(trigger.text()).toBe('AL')
    expect(trigger.attributes('aria-label')).toBe('Ada Lovelace')
    expect(wrapper.find('[data-testid="ui-popover-panel"]').exists()).toBe(false)
  })

  it('opens a panel with the name, the e-mail, every link and Sign out', async () => {
    const wrapper = mountClient(AccountMenu, { props: PROPS })

    await wrapper.find('[data-testid="client-account-trigger"]').trigger('click')

    expect(wrapper.find('[data-testid="client-account-name"]').text()).toBe('Ada Lovelace')
    expect(wrapper.find('[data-testid="client-account-email"]').text()).toBe('ada@example.com')
    expect(wrapper.find('[data-testid="client-account-link-/client/dashboard"]').attributes('href')).toBe('/client/dashboard')
    expect(wrapper.find('[data-testid="client-account-link-/client/account"]').text()).toContain('Account')
    expect(wrapper.find('[data-testid="client-account-sign-out"]').exists()).toBe(true)
  })

  it('falls back to the generic label before the profile arrives', () => {
    const wrapper = mountClient(AccountMenu, { props: { items: [] } })

    expect(wrapper.find('[data-testid="client-account-trigger"]').attributes('aria-label')).toBe('nav.clientArea')
  })

  it('closes the panel when a link is chosen', async () => {
    const wrapper = mountClient(AccountMenu, { props: PROPS })
    await wrapper.find('[data-testid="client-account-trigger"]').trigger('click')

    await wrapper.find('[data-testid="client-account-link-/client/dashboard"]').trigger('click')

    expect(wrapper.find('[data-testid="ui-popover-panel"]').exists()).toBe(false)
  })

  it('emits signOut and closes the panel when Sign out is chosen', async () => {
    const wrapper = mountClient(AccountMenu, { props: PROPS })
    await wrapper.find('[data-testid="client-account-trigger"]').trigger('click')

    await wrapper.find('[data-testid="client-account-sign-out"]').trigger('click')

    expect(wrapper.emitted('signOut')).toHaveLength(1)
    expect(wrapper.find('[data-testid="ui-popover-panel"]').exists()).toBe(false)
  })

  it('hides the docs link when docsUrl is empty', async () => {
    vi.stubGlobal('useRuntimeConfig', () => ({ public: { docsUrl: '' } }))
    const wrapper = mountClient(AccountMenu, { props: PROPS })
    await wrapper.find('[data-testid="client-account-trigger"]').trigger('click')

    expect(wrapper.find('[data-testid="client-account-help"]').exists()).toBe(false)
  })

  it('shows the docs link with the correct href when docsUrl is set', async () => {
    vi.stubGlobal('useRuntimeConfig', () => ({ public: { docsUrl: 'https://docs.example.com' } }))
    const wrapper = mountClient(AccountMenu, { props: PROPS })
    await wrapper.find('[data-testid="client-account-trigger"]').trigger('click')

    const link = wrapper.find('[data-testid="client-account-help"]')
    expect(link.exists()).toBe(true)
    expect(link.attributes('href')).toBe('https://docs.example.com')
    expect(link.attributes('rel')).toContain('noopener')
  })
})
