/**
 * Mount tests for ServicesListView: rows render with price, cycle, status label and a client
 * link, paging asks the API for the next page and keeps it in the address, and a refused load or an
 * empty result shows the right state.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import ServicesListView from '@/views/ServicesListView.vue'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'
import { json, problem } from '../helpers/fetch'
import { BillingCycle } from '@/types/service'

/** One active service as the list endpoint returns it. */
const service = {
  id: 11,
  clientId: 3,
  clientName: 'Alan Turing',
  productName: 'VPS Small',
  domain: null,
  price: 4.99,
  priceCurrency: 'USD',
  billingCycle: BillingCycle.Monthly,
  status: 'Suspended',
  nextDueDate: '2026-05-01T00:00:00Z',
}

/**
 * Mounts the list at an address and waits for its first load.
 *
 * @param url - The address to open, with its query.
 * @returns The wrapper and the router.
 */
const mountListAt = async (url: string) => {
  const screen = await mountRouted(ServicesListView, '/services', url)
  await flushPromises()
  return screen
}

/**
 * Mounts the list at its plain address and waits for its first load.
 *
 * @returns The wrapper.
 */
const mountList = async () => (await mountListAt('/services')).wrapper

describe('ServicesListView', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('renders a row with product, price, capitalised cycle, status and client link', async () => {
    stubRoutes({ 'GET /api/services': () => json({ items: [service], totalCount: 1 }) })
    const wrapper = await mountList()

    const row = wrapper.get('[data-testid="services-row-11"]')
    expect(row.text()).toContain('VPS Small')
    expect(row.text()).toContain('$4.99 USD')
    expect(row.text()).toContain('Monthly')
    expect(row.text()).toContain('Suspended')
    expect(wrapper.get('[data-testid="services-client-11"]').attributes('href')).toBe('/clients/3')
  })

  it('loads the next page from the pager', async () => {
    const mock = stubRoutes({ 'GET /api/services': () => json({ items: [service], totalCount: 45 }) })
    const wrapper = await mountList()

    await wrapper.get('[data-testid="services-pagination-next"]').trigger('click')
    await flushPromises()

    const urls = mock.mock.calls.map(([url]) => url as string)
    expect(urls.some((url) => url.includes('page=2'))).toBe(true)
  })

  it('keeps the chosen page in the address', async () => {
    stubRoutes({ 'GET /api/services': () => json({ items: [service], totalCount: 45 }) })
    const { wrapper, router } = await mountListAt('/services')

    await wrapper.get('[data-testid="services-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '2' })
  })

  it('opens the page the address names', async () => {
    const mock = stubRoutes({ 'GET /api/services': () => json({ items: [service], totalCount: 45 }) })
    await mountListAt('/services?page=3')

    const urls = mock.mock.calls.map(([url]) => url as string)
    expect(urls[0]).toBe('/api/services?page=3&pageSize=20')
  })

  it('shows the empty state when there are no services', async () => {
    stubRoutes({ 'GET /api/services': () => json({ items: [], totalCount: 0 }) })
    const wrapper = await mountList()

    expect(wrapper.get('[data-testid="services-empty"]').text()).toContain('No services found')
  })

  it('shows the API sentence when the load is refused', async () => {
    stubRoutes({ 'GET /api/services': () => problem('Not allowed.', 403) })
    const wrapper = await mountList()

    expect(wrapper.get('[data-testid="services-error"]').text()).toContain('Not allowed.')
  })

  it('loads again from the retry of a refused load', async () => {
    let calls = 0
    stubRoutes({
      'GET /api/services': () => {
        calls += 1
        return calls === 1 ? problem('Not allowed.', 403) : json({ items: [service], totalCount: 1 })
      },
    })
    const wrapper = await mountList()

    await wrapper.get('[data-testid="services-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="services-row-11"]').text()).toContain('VPS Small')
  })
})
