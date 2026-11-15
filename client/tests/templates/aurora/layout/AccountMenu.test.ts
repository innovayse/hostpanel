/**
 * Unit tests for AccountMenu: the avatar trigger, the panel with the account destinations, sign
 * out, and the profile fetch on mount.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import AccountMenu from '~/templates/aurora/layout/AccountMenu.vue'
import { stubAuroraGlobals } from '../auroraGlobals'
import { CLIENT_ACCOUNT_PATH, CLIENT_DASHBOARD_PATH } from '~/constants/clientRoutes'

/** The customer the session reports. */
const USER = { firstName: 'Ann', lastName: 'Lee', email: 'ann@acme.test' }

describe('AccountMenu', () => {
  it('shows only the avatar trigger, named after the customer, until it is opened', () => {
    stubAuroraGlobals({ user: USER })
    const wrapper = mountWithKit(AccountMenu)

    expect(wrapper.find('[data-testid="account-menu-trigger"]').attributes('aria-label')).toBe('Ann Lee')
    expect(wrapper.find('[data-testid="ui-popover-panel"]').exists()).toBe(false)
  })

  it('fetches the profile once on mount, because the cookie only says a session exists', () => {
    const { fetchUser } = stubAuroraGlobals({ user: USER })
    mountWithKit(AccountMenu)

    expect(fetchUser).toHaveBeenCalledTimes(1)
  })

  it('opens a panel with the name, the e-mail and the five account destinations', async () => {
    stubAuroraGlobals({ user: USER })
    const wrapper = mountWithKit(AccountMenu)

    await wrapper.find('[data-testid="account-menu-trigger"]').trigger('click')

    expect(wrapper.find('[data-testid="account-menu-name"]').text()).toBe('Ann Lee')
    expect(wrapper.text()).toContain('ann@acme.test')
    expect(wrapper.find('[data-testid="account-menu-dashboard"]').attributes('href')).toBe(CLIENT_DASHBOARD_PATH)
    expect(wrapper.find('[data-testid="account-menu-account"]').attributes('href')).toBe(CLIENT_ACCOUNT_PATH)
    expect(wrapper.findAll('[role="menuitem"]')).toHaveLength(6)
  })

  it('gives every row an icon and a left-aligned label row, sign out included', async () => {
    stubAuroraGlobals({ user: USER })
    const wrapper = mountWithKit(AccountMenu)
    await wrapper.find('[data-testid="account-menu-trigger"]').trigger('click')

    for (const item of wrapper.findAll('[role="menuitem"]')) {
      expect(item.find('svg').exists()).toBe(true)
      expect(item.find('.w-full').exists()).toBe(true)
    }
    expect(wrapper.find('[data-testid="account-menu-sign-out"]').classes()).toEqual(expect.arrayContaining(['text-danger', 'hover:bg-acbg']))
  })

  it('shows the customer initials in the trigger once the profile is known', () => {
    stubAuroraGlobals({ user: USER })
    const wrapper = mountWithKit(AccountMenu)

    expect(wrapper.find('[data-testid="account-menu-trigger"]').text()).toBe('AL')
  })

  it('falls back to the generic client-area label before the profile arrives', async () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(AccountMenu)

    await wrapper.find('[data-testid="account-menu-trigger"]').trigger('click')

    expect(wrapper.find('[data-testid="account-menu-name"]').text()).toBe('nav.clientArea')
  })

  it('closes the panel when a destination is chosen', async () => {
    stubAuroraGlobals({ user: USER })
    const wrapper = mountWithKit(AccountMenu)
    await wrapper.find('[data-testid="account-menu-trigger"]').trigger('click')

    await wrapper.find('[data-testid="account-menu-services"]').trigger('click')

    expect(wrapper.find('[data-testid="ui-popover-panel"]').exists()).toBe(false)
  })

  it('signs out and closes the panel', async () => {
    const { signOut } = stubAuroraGlobals({ user: USER })
    const wrapper = mountWithKit(AccountMenu)
    await wrapper.find('[data-testid="account-menu-trigger"]').trigger('click')

    await wrapper.find('[data-testid="account-menu-sign-out"]').trigger('click')

    expect(signOut).toHaveBeenCalledTimes(1)
    expect(wrapper.find('[data-testid="ui-popover-panel"]').exists()).toBe(false)
  })
})
