/**
 * Mount tests for DomainsListView: rows render with status, price and links to the owning
 * client's detail page, the search and the page are kept in the address and sent to the API, and a
 * refused load or an empty result shows the right state.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import DomainsListView from '@/views/DomainsListView.vue'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'
import { json, problem, stubFetch } from '../helpers/fetch'
import { DomainStatus } from '@/types/domain'

/** One active domain as the list endpoint returns it. */
const domain = {
  id: 4,
  clientId: 9,
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

/**
 * Mounts the list at an address and waits for its first load.
 *
 * @param url - The address to open, with its query.
 * @returns The wrapper and the router.
 */
const mountListAt = async (url: string) => {
  const screen = await mountRouted(DomainsListView, '/domains', url)
  await flushPromises()
  return screen
}

/**
 * Mounts the list at its plain address and waits for its first load.
 *
 * @returns The wrapper.
 */
const mountList = async () => (await mountListAt('/domains')).wrapper

describe('DomainsListView', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('renders a row with the domain, price, auto-renew and links under the client', async () => {
    stubRoutes({ 'GET /api/domains': () => json({ items: [domain], totalCount: 1 }) })
    const wrapper = await mountList()

    const row = wrapper.get('[data-testid="domains-row-4"]')
    expect(row.text()).toContain('example.com')
    expect(row.text()).toContain('Grace Hopper')
    expect(row.text()).toContain('$12.99 USD')
    expect(row.text()).toContain('Yes')
    expect(wrapper.get('[data-testid="domains-name-4"]').attributes('href')).toBe('/clients/9/domains/4')
    expect(wrapper.get('[data-testid="domains-view-4"]').attributes('href')).toBe('/clients/9/domains/4')
    expect(wrapper.get('[data-testid="domains-client-4"]').attributes('href')).toBe('/clients/9')
  })

  it('puts the typed search in the address and sends it after the pause', async () => {
    const mock = stubRoutes({ 'GET /api/domains': () => json({ items: [domain], totalCount: 1 }) })
    const { wrapper, router } = await mountListAt('/domains')

    await wrapper.get('[data-testid="domains-search"]').setValue('exam')
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'exam' })
    const urls = mock.mock.calls.map(([url]) => url as string)
    expect(urls.some((url) => url.includes('search=exam'))).toBe(true)
  })

  it('restores the search and the page from the address and sends them', async () => {
    const mock = stubRoutes({ 'GET /api/domains': () => json({ items: [domain], totalCount: 45 }) })
    const { wrapper } = await mountListAt('/domains?search=exam&page=2')

    const urls = mock.mock.calls.map(([url]) => url as string)
    expect(urls[0]).toBe('/api/domains?page=2&pageSize=20&search=exam')
    expect((wrapper.get('[data-testid="domains-search"]').element as HTMLInputElement).value).toBe('exam')
  })

  it('keeps the chosen page in the address', async () => {
    stubRoutes({ 'GET /api/domains': () => json({ items: [domain], totalCount: 45 }) })
    const { wrapper, router } = await mountListAt('/domains')

    await wrapper.get('[data-testid="domains-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '2' })
  })

  it('shows the empty state, and the search hint when the list is searched', async () => {
    stubRoutes({ 'GET /api/domains': () => json({ items: [], totalCount: 0 }) })
    const wrapper = await mountList()

    expect(wrapper.get('[data-testid="domains-empty"]').text()).toContain('No domains found')

    const searched = (await mountListAt('/domains?search=zzz')).wrapper
    expect(searched.get('[data-testid="domains-empty"]').text()).toContain('No domains match your search')
  })

  it('shows the API sentence when the load is refused', async () => {
    stubRoutes({ 'GET /api/domains': () => problem('Not allowed.', 403) })
    const wrapper = await mountList()

    expect(wrapper.get('[data-testid="domains-error"]').text()).toContain('Not allowed.')
  })

  it('loads again from the Retry of a refused load', async () => {
    stubFetch(problem('Not allowed.', 403))
    const wrapper = await mountList()
    await flushPromises()
    const fetchMock = vi.mocked(fetch)
    const before = fetchMock.mock.calls.length

    await wrapper.get('[data-testid="domains-error-retry"]').trigger('click')
    await flushPromises()

    expect(fetchMock.mock.calls.length).toBeGreaterThan(before)
  })
})
