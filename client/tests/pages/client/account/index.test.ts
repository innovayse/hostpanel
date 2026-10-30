/**
 * Smoke test for `pages/client/account/index.vue`: the page composes its header, tab strip and
 * the tab named by the query, and loads what the tabs read.
 *
 * @module tests/pages/client/account/index.test
 */

import { defineComponent, h } from 'vue'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mountPage } from '~/tests/helpers/mountClient'
import { Permission } from '~/composables/usePermissions'

/** Loaders the page triggers, re-created per test. */
const loaders = {
  fetchUser: vi.fn(),
  loadCountries: vi.fn(),
  loadGatewayMethods: vi.fn(),
  loadUsers: vi.fn(),
  loadContacts: vi.fn(),
  loadEmails: vi.fn(),
  loadPaymentMethods: vi.fn(),
}

/** The permissions the signed-in person holds; everything unless a test narrows it. */
const held = { value: Permission.All as number }

vi.mock('~/stores/client', () => ({
  useClientStore: () => ({
    fetchUser: loaders.fetchUser,
    mayDo: (flag: number) => flag === 0 || (held.value & flag) === flag,
  }),
}))
vi.mock('~/stores/catalog', () => ({ useCatalogStore: () => ({ loadCountries: loaders.loadCountries }) }))
vi.mock('~/stores/account', () => ({
  useAccountStore: () => ({
    loadGatewayMethods: loaders.loadGatewayMethods,
    loadUsers: loaders.loadUsers,
    loadContacts: loaders.loadContacts,
    loadEmails: loaders.loadEmails,
    loadPaymentMethods: loaders.loadPaymentMethods,
  }),
}))

const { default: AccountPage } = await import('~/pages/client/account/index.vue')

/**
 * Builds a stand-in for one tab component that renders a marker.
 *
 * @param marker - The `data-testid` the stand-in renders.
 * @returns The component.
 */
const tabStub = (marker: string) => defineComponent({ render: () => h('div', { 'data-testid': marker }) })

/** The tab components the page renders, replaced by markers. */
const TAB_STUBS = {
  AccountProfileTab: tabStub('tab-details'),
  AccountContactsTab: tabStub('tab-contacts'),
  AccountTeamTab: tabStub('tab-users'),
  AccountEmailsTab: tabStub('tab-emails'),
  AccountPaymentMethodsTab: tabStub('tab-payment'),
}

/**
 * Stands in for the route with the given query.
 *
 * @param query - The `?` parameters.
 */
const stubRouteWith = (query: Record<string, string>): void => {
  vi.stubGlobal('useRoute', () => ({ params: {}, query, path: '/client/account', fullPath: '/client/account', meta: {} }))
}

beforeEach(() => {
  held.value = Permission.All
  for (const loader of Object.values(loaders)) {
    loader.mockReset()
    loader.mockResolvedValue(undefined)
  }
  stubRouteWith({})
})

describe('client account page', () => {
  it('renders the page with the details tab by default', async () => {
    const wrapper = await mountPage(AccountPage, { components: TAB_STUBS })

    expect(wrapper.find('[data-testid="account-page"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="tab-details"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="tab-contacts"]').exists()).toBe(false)
  })

  it('opens the tab named by ?tab=', async () => {
    stubRouteWith({ tab: 'emails' })

    const wrapper = await mountPage(AccountPage, { components: TAB_STUBS })

    expect(wrapper.find('[data-testid="tab-emails"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="tab-details"]').exists()).toBe(false)
  })

  it('falls back to the details tab for an unknown ?tab=', async () => {
    stubRouteWith({ tab: 'nope' })

    const wrapper = await mountPage(AccountPage, { components: TAB_STUBS })

    expect(wrapper.find('[data-testid="tab-details"]').exists()).toBe(true)
  })

  it('loads the profile and everything the tabs read', async () => {
    await mountPage(AccountPage, { components: TAB_STUBS })

    for (const loader of Object.values(loaders)) {
      expect(loader).toHaveBeenCalledTimes(1)
    }
  })
})

describe('client account page permissions', () => {
  it('lists only the tabs the person may open', async () => {
    held.value = Permission.ViewEmails | Permission.ViewPayInvoices

    const wrapper = await mountPage(AccountPage, { components: TAB_STUBS })

    expect(wrapper.text()).toContain('client.account.tabDetails')
    expect(wrapper.text()).toContain('client.account.tabEmails')
    expect(wrapper.text()).toContain('client.account.tabPayment')
    expect(wrapper.text()).not.toContain('client.contacts.title')
    expect(wrapper.text()).not.toContain('client.account.tabUsers')
  })

  it('opens the details tab when ?tab= names a tab the person may not open', async () => {
    held.value = Permission.ViewEmails
    stubRouteWith({ tab: 'users' })

    const wrapper = await mountPage(AccountPage, { components: TAB_STUBS })

    expect(wrapper.find('[data-testid="tab-details"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="tab-users"]').exists()).toBe(false)
  })

  it('offers the team tab only with full access', async () => {
    held.value = Permission.All

    const wrapper = await mountPage(AccountPage, { components: TAB_STUBS })

    expect(wrapper.text()).toContain('client.account.tabUsers')
  })

  it('keeps the payment tab listed and openable without the invoices permission, so the tab can say why it offers nothing', async () => {
    held.value = Permission.ViewEmails
    stubRouteWith({ tab: 'payment' })

    const wrapper = await mountPage(AccountPage, { components: TAB_STUBS })

    expect(wrapper.text()).toContain('client.account.tabPayment')
    expect(wrapper.find('[data-testid="tab-payment"]').exists()).toBe(true)
  })
})
