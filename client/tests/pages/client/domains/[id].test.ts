/**
 * Tests for `pages/client/domains/[id].vue` against the real domains store, API composables and
 * transport, with a stub BFF answering on a real socket (nothing of ours is mocked): the panels, the
 * pending lock and the switch state that survives switching panels.
 *
 * @module tests/pages/client/domains/[id].test
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { mountPage, stubClientRuntime } from '~/tests/helpers/mountClient'
import { bodyOf } from '~/tests/helpers/requestBody'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import DomainPage from '~/pages/client/domains/[id].vue'
import { DomainStatus } from '~/types/domainstatus'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The BFF paths the page reads and writes for domain 7. */
const PATHS = {
  domain: '/api/portal/client/domains/7',
  lock: '/api/portal/client/domains/7/lock',
} as const

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/**
 * Builds a domain.
 *
 * @param overrides - Fields to set.
 * @returns The domain.
 */
const domain = (overrides: Record<string, unknown> = {}) => ({
  id: 7, name: 'example.com', status: DomainStatus.Active, registeredAt: '2025-01-01', expiresAt: '2099-01-01', nextDueDate: '2098-12-01',
  firstPaymentAmount: 15, recurringAmount: 12, registrationPeriod: 1, paymentMethod: 'card', priceCurrency: 'USD',
  autoRenew: false, isLocked: true, whoisPrivacy: false,
  management: { dnsManagement: false, emailForwarding: false, nameservers: [], dnsRecords: [], emailForwardingRules: [] },
  ...overrides
})

beforeEach(async () => {
  stubClientRuntime({ params: { id: '7' } })
  table = {
    [`GET ${PATHS.domain}`]: { body: domain() },
  }
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

describe('domain page', () => {
  it('reads this domain and opens on the overview', async () => {
    const wrapper = await mountPage(DomainPage)
    await waitUntil(() => expect(wrapper.find('[data-testid="domain-overview"]').exists()).toBe(true))

    expect(requestsTo(stub.requests, PATHS.domain).map(request => request.method)).toEqual(['GET'])
    expect(wrapper.find('[data-testid="domain-header"] h1').text()).toBe('example.com')
  })

  it.each([
    ['nameservers', 'domain-nameservers'],
    ['lock', 'domain-lock'],
    ['autorenew', 'domain-autorenew'],
    ['contact', 'domain-contact'],
    ['renew', 'domain-renew'],
  ])('opens the %s panel from the menu', async (tab, panel) => {
    const wrapper = await mountPage(DomainPage)
    await waitUntil(() => expect(wrapper.find('[data-testid="domain-overview"]').exists()).toBe(true))

    await wrapper.find(`[data-testid="domain-tab-${tab}"], [data-testid="domain-action-${tab}"]`).trigger('click')

    expect(wrapper.find(`[data-testid="${panel}"]`).exists()).toBe(true)
    expect(wrapper.find('[data-testid="domain-overview"]').exists()).toBe(false)
  })

  it('explains a pending registration and locks the other panels', async () => {
    table[`GET ${PATHS.domain}`] = { body: domain({ status: DomainStatus.PendingRegistration }) }

    const wrapper = await mountPage(DomainPage)
    await waitUntil(() => expect(wrapper.find('[data-testid="domain-pending"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="domain-pending"]').text()).toContain('client.domains.pendingRegistrationDesc')
    expect(wrapper.find('[data-testid="domain-tab-lock"]').attributes('disabled')).toBeDefined()
  })

  it('explains a pending transfer', async () => {
    table[`GET ${PATHS.domain}`] = { body: domain({ status: DomainStatus.PendingTransfer }) }

    const wrapper = await mountPage(DomainPage)
    await waitUntil(() => expect(wrapper.find('[data-testid="domain-pending"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="domain-pending"]').text()).toContain('client.domains.pendingTransferDesc')
  })

  it('warns on the overview when the lock is off and the warning follows a change made on another panel', async () => {
    table[`GET ${PATHS.domain}`] = { body: domain({ isLocked: false }) }
    table[`PUT ${PATHS.lock}`] = { body: {} }
    const wrapper = await mountPage(DomainPage)
    await waitUntil(() => expect(wrapper.find('[data-testid="domain-unlock-warning"]').exists()).toBe(true))

    await wrapper.find('[data-testid="domain-tab-lock"]').trigger('click')
    await wrapper.find('[data-testid="domain-lock-row-toggle"]').trigger('click')
    await waitUntil(() => expect(bodyOf(requestsTo(stub.requests, PATHS.lock)[0])).toEqual({ value: true }))
    await wrapper.find('[data-testid="domain-tab-overview"]').trigger('click')
    // The switch only moves once the API has answered, so the warning goes when the reply has been read.
    await waitUntil(() => expect(wrapper.find('[data-testid="domain-unlock-warning"]').exists()).toBe(false))

    expect(wrapper.find('[data-testid="domain-overview"]').exists()).toBe(true)
  })

  it('says the domain was not found when the read fails', async () => {
    table[`GET ${PATHS.domain}`] = { status: 404, body: { detail: 'No such domain.' } }

    const wrapper = await mountPage(DomainPage)
    await waitUntil(() => expect(wrapper.find('[data-testid="ui-empty-state"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="ui-empty-state"]').text()).toContain('client.domains.notFound')
  })
})
