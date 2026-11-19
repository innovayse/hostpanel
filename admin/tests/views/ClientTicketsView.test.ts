/**
 * Tests for ClientTicketsView: it renders against the real kit and settles on an API refusal, and the search, the
 * status, priority and department filters and the page are kept in the address, sent to the API and shown as
 * removable chips.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import ClientTicketsView from '@/views/ClientTicketsView.vue'
import schema from '@/views/ClientTicketsView.schema'
import { renderedCaptions, schemaCaptions, stubPendingFetch } from '../helpers/schemaTable'
import { TicketPriority, TicketStatus } from '@/types/ticket'
import { mountClientView } from '../helpers/clientViews'
import { applyListFilter } from '../helpers/listFilter'
import { json } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'

/** One ticket row as the client list endpoint returns it. */
const ticket = {
  id: 8,
  subject: 'Refund please',
  status: TicketStatus.Open,
  priority: TicketPriority.Medium,
  createdAt: '2026-10-01T00:00:00Z',
  replyCount: 0,
  departmentName: 'Billing',
  lastReplyAt: null,
  isFlagged: false,
  clientId: 7,
  clientName: 'Ada Lovelace',
}

/** The support departments the department filter offers. */
const departments = [
  { id: 1, name: 'Support', email: 'support@example.test' },
  { id: 2, name: 'Billing', email: 'billing@example.test' },
]

/**
 * Stubs the endpoints the tab calls for client 7.
 *
 * @param items - The tickets the list answers with.
 * @param totalCount - The total the list answers with.
 * @returns The mock, for asserting the requests made.
 */
const stubTab = (items: unknown[] = [ticket], totalCount = items.length): ReturnType<typeof vi.fn> =>
  stubRoutes({
    'GET /api/tickets/client/7/stats': () => json({ openedThisMonth: 1, openedLastMonth: 0, openedThisYear: 4, openedLastYear: 2 }),
    'GET /api/tickets/client/7': () => json({ items, totalCount }),
    'GET /api/tickets/departments': () => json(departments),
  })

/**
 * Mounts the tab for client 7 at an address and waits for its first load.
 *
 * @param query - The query string to open, with its leading question mark or empty.
 * @returns The wrapper and the router.
 */
const mountTabAt = async (query = '') => {
  const screen = await mountRouted(ClientTicketsView, '/clients/:id/tickets', `/clients/7/tickets${query}`)
  await flushPromises()
  return screen
}

/**
 * Lists the requests the mock has seen for the client's ticket list.
 *
 * @param mock - The stubbed fetch.
 * @returns The request URLs that went to the list endpoint.
 */
const listUrls = (mock: ReturnType<typeof vi.fn>): string[] =>
  mock.mock.calls.map(([url]) => url as string).filter((url) => url.startsWith('/api/tickets/client/7?'))

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ClientTicketsView', () => {
  it('mounts without throwing', async () => {
    const wrapper = await mountClientView(ClientTicketsView)

    expect(wrapper.exists()).toBe(true)
  })

  it('renders its main action', async () => {
    const wrapper = await mountClientView(ClientTicketsView)

    expect(wrapper.find('[data-testid="client-tickets-new"]').exists()).toBe(true)
  })

  it('shows the refusal sentence from the API', async () => {
    const wrapper = await mountClientView(ClientTicketsView, 'Server said no')

    expect(wrapper.text()).toContain('Server said no')
  })

  it('loads again from the Retry of a refused load', async () => {
    const wrapper = await mountClientView(ClientTicketsView)
    await flushPromises()
    const fetchMock = vi.mocked(fetch)
    const before = fetchMock.mock.calls.length

    await wrapper.get('[data-testid="client-tickets-error-retry"]').trigger('click')
    await flushPromises()

    expect(fetchMock.mock.calls.length).toBeGreaterThan(before)
  })

  it('lists the tickets of the client with department, priority and status', async () => {
    stubTab()

    const { wrapper } = await mountTabAt()
    const row = wrapper.get('[data-testid="client-tickets-row-8"]')

    expect(row.text()).toContain('Refund please')
    expect(row.text()).toContain('Billing')
    expect(row.text()).toContain('Medium')
    expect(row.text()).toContain('Open')
  })

  it('shows the opened-tickets tiles from the schema captions and the stats the API answered', async () => {
    stubTab()

    const { wrapper } = await mountTabAt()

    expect(wrapper.get('[data-testid="client-tickets-stat-openedThisYear"]').text()).toContain('4')
    expect(wrapper.get('[data-testid="client-tickets-stat-openedLastYear"]').text()).toContain('2')
  })

  it('puts the typed search in the address and sends it after the pause', async () => {
    const mock = stubTab()
    const { wrapper, router } = await mountTabAt()

    await wrapper.get('[data-testid="client-tickets-search"]').setValue('refund')
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'refund' })
    expect(listUrls(mock).at(-1)).toBe('/api/tickets/client/7?page=1&pageSize=20&search=refund')
  })

  it('sends the status picked in the filter dialog on Apply and shows it as a chip', async () => {
    const mock = stubTab()
    const { wrapper, router } = await mountTabAt()

    await applyListFilter(wrapper, { 'client-tickets-filter-status': TicketStatus.Closed }, 'client-tickets-filter')

    expect(router.currentRoute.value.query).toEqual({ status: 'Closed' })
    expect(listUrls(mock).at(-1)).toBe('/api/tickets/client/7?page=1&pageSize=20&status=Closed')
    expect(wrapper.get('[data-testid="filter-chip-status"]').text()).toContain('Closed')
  })

  it('sends the priority and the department picked in the filter dialog', async () => {
    const mock = stubTab()
    const { wrapper, router } = await mountTabAt()

    await applyListFilter(wrapper, { 'client-tickets-filter-priority': TicketPriority.High }, 'client-tickets-filter')
    await applyListFilter(wrapper, { 'client-tickets-filter-department': '2' }, 'client-tickets-filter')

    expect(router.currentRoute.value.query).toEqual({ priority: 'High', departmentId: '2' })
    expect(listUrls(mock).at(-1)).toBe('/api/tickets/client/7?page=1&pageSize=20&priority=High&departmentId=2')
    expect(wrapper.get('[data-testid="filter-chip-priority"]').text()).toContain('High')
    expect(wrapper.get('[data-testid="filter-chip-departmentId"]').text()).toContain('Billing')
  })

  it('restores the filters, the search and the page from the address and sends them together', async () => {
    const mock = stubTab([ticket], 45)
    const { wrapper } = await mountTabAt('?search=refund&status=Open&priority=Medium&departmentId=2&page=2')

    expect(listUrls(mock)[0]).toBe('/api/tickets/client/7?page=2&pageSize=20&search=refund&status=Open&priority=Medium&departmentId=2')
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('3')
  })

  it('does not send a filter the address holds that is not a known value', async () => {
    const mock = stubTab()
    const { wrapper } = await mountTabAt('?status=Answered&priority=Urgent&departmentId=abc')

    expect(listUrls(mock)[0]).toBe('/api/tickets/client/7?page=1&pageSize=20')
    expect(wrapper.find('[data-testid="filter-chips"]').exists()).toBe(false)
  })

  it('removes a filter from the address and reloads when its chip is removed', async () => {
    const mock = stubTab()
    const { wrapper, router } = await mountTabAt('?status=Closed&priority=High')

    await wrapper.get('[data-testid="filter-chip-remove-status"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ priority: 'High' })
    expect(listUrls(mock).at(-1)).toBe('/api/tickets/client/7?page=1&pageSize=20&priority=High')
  })

  it('keeps the chosen page in the address and loads it', async () => {
    const mock = stubTab([ticket], 45)
    const { wrapper, router } = await mountTabAt()

    await wrapper.get('[data-testid="client-tickets-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '2' })
    expect(listUrls(mock).at(-1)).toBe('/api/tickets/client/7?page=2&pageSize=20')
  })

  it('shows the empty state, and the filter hint with a way to clear the filters when the list is narrowed', async () => {
    stubTab([])
    const plain = (await mountTabAt()).wrapper
    expect(plain.get('[data-testid="client-tickets-empty"]').text()).toContain('No data available in table')
    expect(plain.find('[data-testid="client-tickets-clear-empty"]').exists()).toBe(false)

    const { wrapper, router } = await mountTabAt('?status=Closed&search=zzz')
    expect(wrapper.get('[data-testid="client-tickets-empty"]').text()).toContain('No tickets match your filters')

    await wrapper.get('[data-testid="client-tickets-clear-empty"]').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.query).toEqual({ search: 'zzz' })
  })

  it('draws the table head from the schema columns', async () => {
    stubTab()
    const { wrapper } = await mountTabAt()

    expect(renderedCaptions(wrapper)).toEqual(schemaCaptions(schema.table.columns))
  })

  it('draws the table skeleton while the first load is in flight', async () => {
    stubPendingFetch()
    const { wrapper } = await mountRouted(ClientTicketsView, '/clients/:id/tickets', '/clients/7/tickets')
    await flushPromises()

    expect(wrapper.find('[data-testid="ui-table-skeleton"]').exists()).toBe(true)
    expect(renderedCaptions(wrapper)).toEqual(schemaCaptions(schema.table.columns))
  })
})
