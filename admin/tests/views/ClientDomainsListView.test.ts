/**
 * Mount tests for ClientDomainsListView: it renders against the real kit, settles on an API refusal, and keeps
 * its page in the address.
 */
import { describe, expect, it, vi } from 'vitest'
import ClientDomainsListView from '@/views/ClientDomainsListView.vue'
import { mountClientView } from '../helpers/clientViews'
import { flushPromises } from '@vue/test-utils'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'
import { json } from '../helpers/fetch'
import { DomainStatus } from '@/types/domain'

/** One domain of the client as the list endpoint returns it. */
const domain = {
  id: 4,
  clientId: 5,
  clientName: 'Grace Hopper',
  name: 'example.com',
  regPeriodYears: 1,
  registrar: null,
  recurringAmount: 12.99,
  priceCurrency: 'USD',
  nextDueDate: '2027-01-01T00:00:00Z',
  expiresAt: '2027-01-01T00:00:00Z',
  autoRenew: true,
  status: DomainStatus.Active,
}

describe('ClientDomainsListView', () => {
  it('mounts without throwing', async () => {
    const wrapper = await mountClientView(ClientDomainsListView)

    expect(wrapper.exists()).toBe(true)
  })

  it('shows the refusal sentence from the API', async () => {
    const wrapper = await mountClientView(ClientDomainsListView, 'Server said no')

    expect(wrapper.text()).toContain('Server said no')
  })

  it('opens the page the address names and keeps the chosen page in the address', async () => {
    const mock = stubRoutes({ 'GET /api/domains': () => json({ items: [domain], totalCount: 45 }) })
    const { wrapper, router } = await mountRouted(ClientDomainsListView, '/clients/:id/domains', '/clients/5/domains?page=2')
    await flushPromises()

    expect(mock.mock.calls.map(([url]) => url as string)[0]).toBe('/api/domains?clientId=5&page=2&pageSize=20')
    await wrapper.get('[data-testid="client-domains-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '3' })
    expect(mock.mock.calls.map(([url]) => url as string).at(-1)).toBe('/api/domains?clientId=5&page=3&pageSize=20')
  })

  it('loads again from the Retry of a refused load', async () => {
    const wrapper = await mountClientView(ClientDomainsListView)
    await flushPromises()
    const fetchMock = vi.mocked(fetch)
    const before = fetchMock.mock.calls.length

    await wrapper.get('[data-testid="client-domains-error-retry"]').trigger('click')
    await flushPromises()

    expect(fetchMock.mock.calls.length).toBeGreaterThan(before)
  })
})
