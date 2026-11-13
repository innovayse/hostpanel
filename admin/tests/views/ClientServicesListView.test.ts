/**
 * Mount tests for ClientServicesListView: it renders against the real kit, settles on an API refusal, and keeps
 * its page in the address.
 */
import { describe, expect, it, vi } from 'vitest'
import ClientServicesListView from '@/views/ClientServicesListView.vue'
import { mountClientView } from '../helpers/clientViews'
import { flushPromises } from '@vue/test-utils'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'
import { json } from '../helpers/fetch'
import { BillingCycle, ServiceStatus } from '@/types/service'

/** One service of the client as the list endpoint returns it. */
const service = {
  id: 11,
  clientId: 5,
  clientName: 'Alan Turing',
  productName: 'VPS Small',
  domain: null,
  price: 4.99,
  priceCurrency: 'USD',
  billingCycle: BillingCycle.Monthly,
  status: ServiceStatus.Active,
  nextDueDate: '2026-05-01T00:00:00Z',
}

describe('ClientServicesListView', () => {
  it('mounts without throwing', async () => {
    const wrapper = await mountClientView(ClientServicesListView)

    expect(wrapper.exists()).toBe(true)
  })

  it('shows the refusal sentence from the API', async () => {
    const wrapper = await mountClientView(ClientServicesListView, 'Server said no')

    expect(wrapper.text()).toContain('Server said no')
  })

  it('opens the page the address names and keeps the chosen page in the address', async () => {
    const mock = stubRoutes({ 'GET /api/services': () => json({ items: [service], totalCount: 45 }) })
    const { wrapper, router } = await mountRouted(ClientServicesListView, '/clients/:id/services', '/clients/5/services?page=2')
    await flushPromises()

    expect(mock.mock.calls.map(([url]) => url as string)[0]).toBe('/api/services?clientId=5&page=2&pageSize=20')
    await wrapper.get('[data-testid="client-services-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '3' })
    expect(mock.mock.calls.map(([url]) => url as string).at(-1)).toBe('/api/services?clientId=5&page=3&pageSize=20')
  })

  it('loads again from the Retry of a refused load', async () => {
    const wrapper = await mountClientView(ClientServicesListView)
    await flushPromises()
    const fetchMock = vi.mocked(fetch)
    const before = fetchMock.mock.calls.length

    await wrapper.get('[data-testid="client-services-error-retry"]').trigger('click')
    await flushPromises()

    expect(fetchMock.mock.calls.length).toBeGreaterThan(before)
  })
})
