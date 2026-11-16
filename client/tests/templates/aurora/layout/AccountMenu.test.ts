/**
 * Unit tests for AccountMenu: the avatar trigger, the panel with the account destinations, sign
 * out, and the profile fetch on mount.
 *
 * The real auth store, API composable and transport run against a stub server playing the BFF.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import AccountMenu from '~/templates/aurora/layout/AccountMenu.vue'
import { useAuroraGlobals } from '../auroraGlobals'
import { AccountEndpoint } from '~/tests/helpers/accountPaths'
import { requestsTo, writeOf } from '~/tests/helpers/stubRoutes'
import { waitUntil } from '~/tests/helpers/waitUntil'
import { CLIENT_ACCOUNT_PATH, CLIENT_DASHBOARD_PATH } from '~/constants/clientRoutes'

/** The customer the session reports. */
const USER = { firstName: 'Ann', lastName: 'Lee', email: 'ann@acme.test' }

/** The stub server and the loader of the aurora globals; the hooks start the stub before each test and close it after. */
const { replies, stub, loadGlobals } = useAuroraGlobals()

/**
 * Mounts the menu of a signed-in customer and waits until the profile read has filled in the name.
 *
 * @returns The wrapper.
 */
const mountSignedIn = async () => {
  await loadGlobals({ user: USER })
  const wrapper = mountWithKit(AccountMenu)
  await waitUntil(() => expect(wrapper.find('[data-testid="account-menu-trigger"]').text()).toBe('AL'))
  return wrapper
}

describe('AccountMenu', () => {
  beforeEach(() => {
    replies[writeOf(AccountEndpoint.Logout)] = { body: {} }
  })

  it('shows only the avatar trigger, named after the customer, until it is opened', async () => {
    const wrapper = await mountSignedIn()

    expect(wrapper.find('[data-testid="account-menu-trigger"]').attributes('aria-label')).toBe('Ann Lee')
    expect(wrapper.find('[data-testid="ui-popover-panel"]').exists()).toBe(false)
  })

  it('fetches the profile once on mount, because the cookie only says a session exists', async () => {
    await mountSignedIn()

    expect(requestsTo(stub().requests, AccountEndpoint.Me)).toHaveLength(1)
  })

  it('opens a panel with the name, the e-mail and the five account destinations', async () => {
    const wrapper = await mountSignedIn()

    await wrapper.find('[data-testid="account-menu-trigger"]').trigger('click')

    expect(wrapper.find('[data-testid="account-menu-name"]').text()).toBe('Ann Lee')
    expect(wrapper.text()).toContain('ann@acme.test')
    expect(wrapper.find('[data-testid="account-menu-dashboard"]').attributes('href')).toBe(CLIENT_DASHBOARD_PATH)
    expect(wrapper.find('[data-testid="account-menu-account"]').attributes('href')).toBe(CLIENT_ACCOUNT_PATH)
    expect(wrapper.findAll('[role="menuitem"]')).toHaveLength(6)
  })

  it('gives every row an icon and a left-aligned label row, sign out included', async () => {
    const wrapper = await mountSignedIn()
    await wrapper.find('[data-testid="account-menu-trigger"]').trigger('click')

    for (const item of wrapper.findAll('[role="menuitem"]')) {
      expect(item.find('svg').exists()).toBe(true)
      expect(item.find('.w-full').exists()).toBe(true)
    }
    expect(wrapper.find('[data-testid="account-menu-sign-out"]').classes()).toEqual(expect.arrayContaining(['text-danger', 'hover:bg-acbg']))
  })

  it('shows the customer initials in the trigger once the profile is known', async () => {
    const wrapper = await mountSignedIn()

    expect(wrapper.find('[data-testid="account-menu-trigger"]').text()).toBe('AL')
  })

  it('falls back to the generic client-area label before the profile arrives', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(AccountMenu)

    await wrapper.find('[data-testid="account-menu-trigger"]').trigger('click')

    expect(wrapper.find('[data-testid="account-menu-name"]').text()).toBe('nav.clientArea')
  })

  it('closes the panel when a destination is chosen', async () => {
    const wrapper = await mountSignedIn()
    await wrapper.find('[data-testid="account-menu-trigger"]').trigger('click')

    await wrapper.find('[data-testid="account-menu-services"]').trigger('click')

    expect(wrapper.find('[data-testid="ui-popover-panel"]').exists()).toBe(false)
  })

  it('signs out and closes the panel', async () => {
    const wrapper = await mountSignedIn()
    await wrapper.find('[data-testid="account-menu-trigger"]').trigger('click')

    await wrapper.find('[data-testid="account-menu-sign-out"]').trigger('click')

    await waitUntil(() => expect(requestsTo(stub().requests, AccountEndpoint.Logout)).toHaveLength(1))
    expect(wrapper.find('[data-testid="ui-popover-panel"]').exists()).toBe(false)
  })
})
