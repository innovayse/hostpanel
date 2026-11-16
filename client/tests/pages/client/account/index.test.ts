/**
 * Tests for `pages/client/account/index.vue` against the real client, catalogue and account stores,
 * the real API composables and the real transport, with a stub BFF answering on a real socket
 * (nothing of ours is mocked, the tabs included): the page composes its header, tab strip and the tab named by the
 * query, and reads what the tabs show.
 *
 * @module tests/pages/client/account/index.test
 */

import type { VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ACCOUNT_COMPONENTS } from '~/tests/helpers/mountAccount'
import { mountRenderedClientPage } from '~/tests/helpers/renderedPage'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { stubLocalePath, stubRoute } from '~/tests/helpers/runtimeStubs'
import { AccountEndpoint } from '~/tests/helpers/accountPaths'
import { BillingEndpoint } from '~/tests/helpers/billingPaths'
import { CatalogPath } from '~/tests/helpers/catalogPaths'
import { readOf, requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { useAuthMode } from '~/composables/useAuthMode'
import { useClientStore } from '~/stores/client'
import { AccountTab } from '~/types/accounttab'
import { AuthMode } from '~/types/authmode'
import { Permission } from '~/types/permission'

const { default: AccountPage } = await import('~/pages/client/account/index.vue')

/** The stub playing the BFF. */
let stub: StubServer

/**
 * How many times a path was read.
 *
 * @param path - The path.
 * @returns The count.
 */
const reads = (path: string): number => requestsTo(stub.requests, path).length

/**
 * Mounts the page for a person holding the given permissions, on the route with the given query.
 *
 * The person is both what the BFF answers for the profile and what the client store already holds
 * when the page starts: the page picks its first tab from the store before the profile read ends.
 *
 * @param permissions - The permissions the person holds.
 * @param query - The `?` parameters of the route.
 * @returns The mounted page, once it has rendered.
 */
const mountAccountPage = async (permissions: number, query: Record<string, string> = {}): Promise<VueWrapper> => {
  const person = makeClientUser({ permissions })
  stub = await startTransport(routeTable({
    [readOf(AccountEndpoint.Me)]: { body: person },
    [readOf(CatalogPath.Countries)]: { body: [] },
    [readOf(BillingEndpoint.OrderPaymentMethods)]: { body: [] },
    [readOf(AccountEndpoint.Users)]: { body: [] },
    [readOf(AccountEndpoint.Contacts)]: { body: [] },
    [readOf(AccountEndpoint.Emails)]: { body: { items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0 } },
  }))
  stubRoute({ query })
  stubLocalePath()
  vi.stubGlobal('useAuthMode', useAuthMode)
  vi.stubGlobal('useRuntimeConfig', () => ({ public: { authMode: AuthMode.Local } }))
  const pinia = createPinia()
  setActivePinia(pinia)
  useClientStore().user = person
  return mountRenderedClientPage(AccountPage, 'account-page', { components: ACCOUNT_COMPONENTS, pinia })
}

afterEach(async () => {
  await stub.close()
})

describe('client account page', () => {
  it('renders the page with the details tab by default', async () => {
    const wrapper = await mountAccountPage(Permission.All)

    expect(wrapper.find('[data-testid="account-page"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="account-profile-tab"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="account-contacts-tab"]').exists()).toBe(false)
  })

  it('opens the tab named by ?tab=', async () => {
    const wrapper = await mountAccountPage(Permission.All, { tab: AccountTab.Emails })

    expect(wrapper.find('[data-testid="account-emails-tab"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="account-profile-tab"]').exists()).toBe(false)
  })

  it('falls back to the details tab for an unknown ?tab=', async () => {
    const wrapper = await mountAccountPage(Permission.All, { tab: 'nope' })

    expect(wrapper.find('[data-testid="account-profile-tab"]').exists()).toBe(true)
  })

  it('loads the profile and everything the tabs read', async () => {
    await mountAccountPage(Permission.All)

    const paths = [
      AccountEndpoint.Me,
      CatalogPath.Countries,
      BillingEndpoint.OrderPaymentMethods,
      AccountEndpoint.Users,
      AccountEndpoint.Contacts,
      AccountEndpoint.Emails,
    ]

    for (const path of paths) {
      expect(reads(path)).toBe(1)
    }
  })
})

describe('client account page permissions', () => {
  it('lists only the tabs the person may open', async () => {
    const wrapper = await mountAccountPage(Permission.ViewEmails | Permission.ViewPayInvoices)

    expect(wrapper.text()).toContain('client.account.tabDetails')
    expect(wrapper.text()).toContain('client.account.tabEmails')
    expect(wrapper.text()).toContain('client.account.tabPayment')
    expect(wrapper.text()).not.toContain('client.contacts.title')
    expect(wrapper.text()).not.toContain('client.account.tabUsers')
  })

  it('opens the details tab when ?tab= names a tab the person may not open', async () => {
    const wrapper = await mountAccountPage(Permission.ViewEmails, { tab: AccountTab.Users })

    expect(wrapper.find('[data-testid="account-profile-tab"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="account-team-tab"]').exists()).toBe(false)
  })

  it('offers the team tab only with full access', async () => {
    const wrapper = await mountAccountPage(Permission.All)

    expect(wrapper.text()).toContain('client.account.tabUsers')
  })

  it('keeps the payment tab listed and openable without the invoices permission, so the tab can say why it offers nothing', async () => {
    const wrapper = await mountAccountPage(Permission.ViewEmails, { tab: AccountTab.Payment })

    expect(wrapper.text()).toContain('client.account.tabPayment')
    expect(wrapper.find('[data-testid="account-payment-tab"]').exists()).toBe(true)
  })
})
