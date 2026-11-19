/**
 * Mount tests for ClientServicesListView: it renders against the real kit, settles on an API refusal, and keeps
 * its search, its status, product and billing cycle filters and its page in the address, sends them to the API and
 * shows each applied filter as a removable chip.
 */
import { describe, expect, it, vi } from 'vitest'
import ClientServicesListView from '@/views/ClientServicesListView.vue'
import { mountClientView } from '../helpers/clientViews'
import { flushPromises } from '@vue/test-utils'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'
import { applyListFilter } from '../helpers/listFilter'
import { json } from '../helpers/fetch'
import { BillingCycle, ServiceStatus } from '@/types/service'
import schema from '@/views/ClientServicesListView.schema'
import { renderedCaptions, schemaCaptions, stubPendingFetch } from '../helpers/schemaTable'

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

/** The products the product filter offers. */
const products = [
  { id: 1, name: 'Shared Starter' },
  { id: 2, name: 'VPS Small' },
]

/**
 * Stubs the list endpoint and the product lookup the filter dialog reads.
 *
 * @param items - The services the list answers with.
 * @param totalCount - The total the list answers with.
 * @returns The mock, for asserting the requests made.
 */
const stubTab = (items: unknown[] = [service], totalCount = items.length): ReturnType<typeof vi.fn> =>
  stubRoutes({
    'GET /api/services': () => json({ items, totalCount }),
    'GET /api/products': () => json(products),
  })

/**
 * Mounts the tab for client 5 at an address and waits for its first load.
 *
 * @param query - The query string to open, with its leading question mark or empty.
 * @returns The wrapper and the router.
 */
const mountTabAt = async (query = '') => {
  const screen = await mountRouted(ClientServicesListView, '/clients/:id/services', `/clients/5/services${query}`)
  await flushPromises()
  return screen
}

/**
 * Lists the requests the mock has seen for the client's service list.
 *
 * @param mock - The stubbed fetch.
 * @returns The request URLs that went to the list endpoint.
 */
const listUrls = (mock: ReturnType<typeof vi.fn>): string[] =>
  mock.mock.calls.map(([url]) => url as string).filter((url) => url.startsWith('/api/services?'))

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

  it('puts the typed search in the address and sends it with the client after the pause', async () => {
    const mock = stubTab()
    const { wrapper, router } = await mountTabAt()

    await wrapper.get('[data-testid="client-services-search"]').setValue('vps')
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'vps' })
    expect(listUrls(mock).at(-1)).toBe('/api/services?clientId=5&page=1&pageSize=20&search=vps')
  })

  it('sends the status, the product and the billing cycle picked in the filter dialog and shows each as a chip', async () => {
    const mock = stubTab()
    const { wrapper, router } = await mountTabAt()

    await applyListFilter(wrapper, { 'client-services-filter-status': 'Active' }, 'client-services-filter')
    expect(listUrls(mock).at(-1)).toBe('/api/services?clientId=5&page=1&pageSize=20&status=Active')

    await applyListFilter(wrapper, { 'client-services-filter-product': '1' }, 'client-services-filter')
    expect(wrapper.get('[data-testid="filter-chip-productId"]').text()).toContain('Shared Starter')

    await applyListFilter(wrapper, { 'client-services-filter-billing-cycle': 'Quarterly' }, 'client-services-filter')
    expect(router.currentRoute.value.query).toEqual({ status: 'Active', productId: '1', billingCycle: 'Quarterly' })
    expect(listUrls(mock).at(-1)).toBe('/api/services?clientId=5&page=1&pageSize=20&status=Active&productId=1&billingCycle=Quarterly')
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('3')
  })

  it('restores the filters, the search and the page from the address and sends them together', async () => {
    const mock = stubTab([service], 45)
    await mountTabAt('?search=vps&status=Suspended&productId=2&billingCycle=Annual&page=2')

    expect(listUrls(mock)[0]).toBe('/api/services?clientId=5&page=2&pageSize=20&search=vps&status=Suspended&productId=2&billingCycle=Annual')
  })

  it('removes a filter from the address and reloads when its chip is removed', async () => {
    const mock = stubTab()
    const { wrapper, router } = await mountTabAt('?status=Active&billingCycle=Monthly')

    await wrapper.get('[data-testid="filter-chip-remove-billingCycle"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ status: 'Active' })
    expect(listUrls(mock).at(-1)).toBe('/api/services?clientId=5&page=1&pageSize=20&status=Active')
  })

  it('names the empty list after the search or filters, and clears the filters from it', async () => {
    stubTab([])
    const plain = await mountTabAt()
    expect(plain.wrapper.get('[data-testid="client-services-empty"]').text()).toContain('No services found for this client')
    expect(plain.wrapper.find('[data-testid="client-services-clear-empty"]').exists()).toBe(false)

    const narrowed = await mountTabAt('?status=Active&search=zzz')
    expect(narrowed.wrapper.get('[data-testid="client-services-empty"]').text()).toContain('No services match your filters')

    await narrowed.wrapper.get('[data-testid="client-services-clear-empty"]').trigger('click')
    await flushPromises()

    expect(narrowed.router.currentRoute.value.query).toEqual({ search: 'zzz' })
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

  it('draws the table head from the schema columns', async () => {
    stubTab()
    const { wrapper } = await mountTabAt()

    expect(renderedCaptions(wrapper)).toEqual(schemaCaptions(schema.table.columns))
  })

  it('draws the table skeleton while the first load is in flight', async () => {
    stubPendingFetch()
    const { wrapper } = await mountRouted(ClientServicesListView, '/clients/:id/services', '/clients/5/services')
    await flushPromises()

    expect(wrapper.find('[data-testid="ui-table-skeleton"]').exists()).toBe(true)
    expect(renderedCaptions(wrapper)).toEqual(schemaCaptions(schema.table.columns))
  })
})
