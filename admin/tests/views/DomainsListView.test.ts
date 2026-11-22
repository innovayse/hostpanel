/**
 * Mount tests for DomainsListView: rows render with registrar, status, price and links to the owning
 * client's detail page, the search, the status and registrar filters and the page are kept in the address and
 * sent to the API, each applied filter shows as a removable chip, and a refused load or an empty result shows the
 * right state.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import DomainsListView from '@/views/DomainsListView.vue'
import schema from '@/views/DomainsListView.schema'
import { renderedCaptions, schemaCaptions } from '../helpers/schemaTable'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'
import { json, problem, stubFetch } from '../helpers/fetch'
import { applyListFilter } from '../helpers/listFilter'
import { DomainStatus } from '@/types/domain'

/** The installed registrar plugins every list screen reads on mount, as the plugins endpoint answers them. */
const REGISTRAR_ROUTES = {
  'GET /api/admin/plugins': () =>
    json([
      { id: 'registrar-one', name: 'Registrar One', type: 'registrar', isLoaded: true, status: 'Loaded' },
      { id: 'registrar-two', name: 'Registrar Two', type: 'registrar', isLoaded: true, status: 'Loaded' },
    ]),
}

/** One active domain as the list endpoint returns it. */
const domain = {
  id: 4,
  clientId: 9,
  clientName: 'Grace Hopper',
  name: 'example.com',
  regPeriodYears: 1,
  registrar: 'Example Registrar',
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

/**
 * Lists the requests the mock has seen for the domain list.
 *
 * @param mock - The stubbed fetch.
 * @returns The request URLs that went to the list endpoint.
 */
const listUrls = (mock: ReturnType<typeof vi.fn>): string[] =>
  mock.mock.calls.map(([url]) => url as string).filter((url) => url.startsWith('/api/domains?'))

describe('DomainsListView', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('renders a row with the domain, price, auto-renew and links under the client', async () => {
    stubRoutes({ ...REGISTRAR_ROUTES, 'GET /api/domains': () => json({ items: [domain], totalCount: 1 }) })
    const wrapper = await mountList()

    const row = wrapper.get('[data-testid="domains-row-4"]')
    expect(row.text()).toContain('example.com')
    expect(row.text()).toContain('Grace Hopper')
    expect(row.text()).toContain('$12.99 USD')
    expect(row.get('[role="img"]').attributes('aria-label')).toBe('Yes')
    expect(wrapper.findAll('th').find((header) => header.text() === 'Domain Name')?.classes()).toContain('w-full')
    expect(row.text()).toContain('Registrar One')
    expect(wrapper.get('[data-testid="domains-name-4"]').attributes('href')).toBe('/clients/9/domains/4')
    expect(wrapper.get('[data-testid="domains-view-4"]').attributes('href')).toBe('/clients/9/domains/4')
    expect(wrapper.get('[data-testid="domains-client-4"]').attributes('href')).toBe('/clients/9')
  })

  it('draws the headers of the schema columns, in order', async () => {
    stubRoutes({ ...REGISTRAR_ROUTES, 'GET /api/domains': () => json({ items: [domain], totalCount: 1 }) })
    const wrapper = await mountList()

    expect(renderedCaptions(wrapper)).toEqual(schemaCaptions(schema.table.columns))
  })

  it('puts the typed search in the address and sends it after the pause', async () => {
    const mock = stubRoutes({ ...REGISTRAR_ROUTES, 'GET /api/domains': () => json({ items: [domain], totalCount: 1 }) })
    const { wrapper, router } = await mountListAt('/domains')

    await wrapper.get('[data-testid="domains-search"]').setValue('exam')
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'exam' })
    const urls = mock.mock.calls.map(([url]) => url as string)
    expect(urls.some((url) => url.includes('search=exam'))).toBe(true)
  })

  it('restores the search and the page from the address and sends them', async () => {
    const mock = stubRoutes({ ...REGISTRAR_ROUTES, 'GET /api/domains': () => json({ items: [domain], totalCount: 45 }) })
    const { wrapper } = await mountListAt('/domains?search=exam&page=2')

    const urls = mock.mock.calls.map(([url]) => url as string)
    expect(urls[0]).toBe('/api/domains?page=2&pageSize=20&search=exam')
    expect((wrapper.get('[data-testid="domains-search"]').element as HTMLInputElement).value).toBe('exam')
  })

  it('keeps the chosen page in the address', async () => {
    stubRoutes({ ...REGISTRAR_ROUTES, 'GET /api/domains': () => json({ items: [domain], totalCount: 45 }) })
    const { wrapper, router } = await mountListAt('/domains')

    await wrapper.get('[data-testid="domains-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '2' })
  })

  it('sends the status picked in the filter dialog on Apply and shows it as a chip', async () => {
    const mock = stubRoutes({ ...REGISTRAR_ROUTES, 'GET /api/domains': () => json({ items: [domain], totalCount: 1 }) })
    const { wrapper, router } = await mountListAt('/domains')

    await applyListFilter(wrapper, { 'domains-filter-status': DomainStatus.Expired })

    expect(router.currentRoute.value.query).toEqual({ status: 'Expired' })
    expect(listUrls(mock).at(-1)).toBe('/api/domains?page=1&pageSize=20&status=Expired')
    expect(wrapper.get('[data-testid="filter-chip-status"]').text()).toContain('Expired')
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('1')
  })

  it('sends the registrar picked in the filter dialog on Apply', async () => {
    const mock = stubRoutes({ ...REGISTRAR_ROUTES, 'GET /api/domains': () => json({ items: [domain], totalCount: 1 }) })
    const { wrapper, router } = await mountListAt('/domains')

    await applyListFilter(wrapper, { 'domains-filter-registrar': 'registrar-two' })

    expect(router.currentRoute.value.query).toEqual({ registrarModule: 'registrar-two' })
    expect(listUrls(mock).at(-1)).toBe('/api/domains?page=1&pageSize=20&registrarModule=registrar-two')
    expect(wrapper.get('[data-testid="filter-chip-registrarModule"]').text()).toContain('Registrar Two')
  })

  it('restores the filters, the search and the page from the address and sends them together', async () => {
    const mock = stubRoutes({ ...REGISTRAR_ROUTES, 'GET /api/domains': () => json({ items: [domain], totalCount: 45 }) })
    const { wrapper } = await mountListAt('/domains?search=exam&status=Active&registrarModule=registrar-one&page=2')

    expect(listUrls(mock)[0]).toBe('/api/domains?page=2&pageSize=20&search=exam&status=Active&registrarModule=registrar-one')
    expect(wrapper.get('[data-testid="filter-chip-status"]').text()).toContain('Active')
    expect(wrapper.get('[data-testid="filter-chip-registrarModule"]').text()).toContain('Registrar One')
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('2')
  })

  it('does not send a status the address holds that is not a known value, and sends the registrar as written', async () => {
    const mock = stubRoutes({ ...REGISTRAR_ROUTES, 'GET /api/domains': () => json({ items: [domain], totalCount: 1 }) })
    await mountListAt('/domains?status=Nonsense&registrarModule=99')

    expect(listUrls(mock)[0]).toBe('/api/domains?page=1&pageSize=20&registrarModule=99')
  })

  it('removes a filter from the address and reloads when its chip is removed', async () => {
    const mock = stubRoutes({ ...REGISTRAR_ROUTES, 'GET /api/domains': () => json({ items: [domain], totalCount: 1 }) })
    const { wrapper, router } = await mountListAt('/domains?status=Active&registrarModule=registrar-one')
    const before = listUrls(mock).length

    await wrapper.get('[data-testid="filter-chip-remove-status"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ registrarModule: 'registrar-one' })
    expect(listUrls(mock).length).toBeGreaterThan(before)
    expect(listUrls(mock).at(-1)).toBe('/api/domains?page=1&pageSize=20&registrarModule=registrar-one')
  })

  it('shows the empty state, and the filter hint when the list is narrowed', async () => {
    stubRoutes({ ...REGISTRAR_ROUTES, 'GET /api/domains': () => json({ items: [], totalCount: 0 }) })
    const wrapper = await mountList()

    expect(wrapper.get('[data-testid="domains-empty"]').text()).toContain('No domains found')
    expect(wrapper.find('[data-testid="domains-clear-empty"]').exists()).toBe(false)

    const searched = (await mountListAt('/domains?search=zzz')).wrapper
    expect(searched.get('[data-testid="domains-empty"]').text()).toContain('No domains match your filters')
    expect(searched.find('[data-testid="domains-clear-empty"]').exists()).toBe(false)
  })

  it('clears the filters from the empty state and keeps the search', async () => {
    stubRoutes({ ...REGISTRAR_ROUTES, 'GET /api/domains': () => json({ items: [], totalCount: 0 }) })
    const { wrapper, router } = await mountListAt('/domains?status=Active&search=zzz')

    await wrapper.get('[data-testid="domains-clear-empty"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'zzz' })
  })

  it('shows the API sentence when the load is refused', async () => {
    stubRoutes({ ...REGISTRAR_ROUTES, 'GET /api/domains': () => problem('Not allowed.', 403) })
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
