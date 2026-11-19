/**
 * Mount tests for ClientLogView: it renders against the real kit, settles on an API refusal with a Retry, and the
 * search, the period and the page are kept in the address, sent to the API (which narrows the entries before it pages)
 * and shown as removable chips.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import ClientLogView from '@/views/ClientLogView.vue'
import schema from '@/views/ClientLogView.schema'
import { renderedCaptions, schemaCaptions, stubPendingFetch } from '../helpers/schemaTable'
import { mountClientView } from '../helpers/clientViews'
import { json } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'

/** One entry of the log of client 7. */
const entry = {
  id: 3,
  description: 'Updated the profile',
  adminName: 'Ada Admin',
  adminEmail: 'ada@example.test',
  ipAddress: '10.0.0.5',
  createdAt: '2026-03-10T12:00:00Z',
}

/** The one-entry log of client 7. */
const log = () => json({ items: [entry], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 })

/**
 * Mounts the screen for client 7 at an address and waits for its first load.
 *
 * @param query - The query string to open, with its leading question mark or empty.
 * @returns The wrapper and the router.
 */
const mountLogAt = async (query = '') => {
  const screen = await mountRouted(ClientLogView, '/clients/:id/log', `/clients/7/log${query}`)
  await flushPromises()
  return screen
}

/**
 * Lists the requests the mock has seen for the log of the client.
 *
 * @param mock - The stubbed fetch.
 * @returns The request URLs that went to the list endpoint.
 */
const listUrls = (mock: ReturnType<typeof vi.fn>): string[] =>
  mock.mock.calls.map(([url]) => url as string).filter((url) => url.startsWith('/api/clients/7/activity-logs?'))

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ClientLogView', () => {
  it('mounts without throwing', async () => {
    const wrapper = await mountClientView(ClientLogView)

    expect(wrapper.exists()).toBe(true)
  })

  it('renders the search box and the filter button', async () => {
    const wrapper = await mountClientView(ClientLogView)

    expect(wrapper.find('[data-testid="client-log-search"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="client-log-filter"]').exists()).toBe(true)
  })

  it('shows the refusal sentence from the API', async () => {
    const wrapper = await mountClientView(ClientLogView, 'Server said no')

    expect(wrapper.text()).toContain('Server said no')
  })

  it('loads again from the Retry of a refused load', async () => {
    const wrapper = await mountClientView(ClientLogView)
    await flushPromises()
    const fetchMock = vi.mocked(fetch)
    const before = fetchMock.mock.calls.length

    await wrapper.get('[data-testid="client-log-error-retry"]').trigger('click')
    await flushPromises()

    expect(fetchMock.mock.calls.length).toBeGreaterThan(before)
  })

  it('lists the entries the API returns', async () => {
    const mock = stubRoutes({ 'GET /api/clients/7/activity-logs': log })
    const { wrapper } = await mountLogAt()

    expect(listUrls(mock)[0]).toBe('/api/clients/7/activity-logs?page=1&pageSize=25')
    expect(wrapper.get('[data-testid="client-log-table"]').text()).toContain('Updated the profile')
    expect(wrapper.get('[data-testid="client-log-count"]').text()).toContain('1')
  })

  it('puts the typed search in the address and sends it after the pause', async () => {
    const mock = stubRoutes({ 'GET /api/clients/7/activity-logs': log })
    const { wrapper, router } = await mountLogAt()

    await wrapper.get('[data-testid="client-log-search"]').setValue('10.0.0')
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: '10.0.0' })
    expect(listUrls(mock).at(-1)).toBe('/api/clients/7/activity-logs?page=1&pageSize=25&search=10.0.0')
  })

  it('restores the search, the period and the page from the address and sends the period as days', async () => {
    const mock = stubRoutes({ 'GET /api/clients/7/activity-logs': log })
    const { wrapper } = await mountLogAt('?search=ada&from=2026-03-01&to=2026-03-31&page=2')

    expect(listUrls(mock)[0]).toBe('/api/clients/7/activity-logs?page=2&pageSize=25&search=ada&from=2026-03-01&to=2026-03-31')
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('2')
    expect(wrapper.find('[data-testid="filter-chip-from"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="filter-chip-to"]').exists()).toBe(true)
  })

  it('does not send a period the address holds that is not a day', async () => {
    const mock = stubRoutes({ 'GET /api/clients/7/activity-logs': log })
    const { wrapper } = await mountLogAt('?from=soon&to=2026-03-31T10%3A00%3A00Z')

    expect(listUrls(mock)[0]).toBe('/api/clients/7/activity-logs?page=1&pageSize=25')
    expect(wrapper.find('[data-testid="filter-chips"]').exists()).toBe(false)
  })

  it('removes the period end from the address and reloads when its chip is removed', async () => {
    const mock = stubRoutes({ 'GET /api/clients/7/activity-logs': log })
    const { wrapper, router } = await mountLogAt('?from=2026-03-01&to=2026-03-31')

    await wrapper.get('[data-testid="filter-chip-remove-to"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ from: '2026-03-01' })
    expect(listUrls(mock).at(-1)).toBe('/api/clients/7/activity-logs?page=1&pageSize=25&from=2026-03-01')
  })

  it('keeps the chosen page in the address and loads it', async () => {
    const mock = stubRoutes({
      'GET /api/clients/7/activity-logs': () => json({ items: [entry], pageNumber: 1, pageSize: 25, totalCount: 60, totalPages: 3 }),
    })
    const { wrapper, router } = await mountLogAt()

    await wrapper.get('[data-testid="client-log-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '2' })
    expect(listUrls(mock).at(-1)).toBe('/api/clients/7/activity-logs?page=2&pageSize=25')
  })

  it('draws the table head from the schema columns', async () => {
    stubRoutes({ 'GET /api/clients/7/activity-logs': log })
    const { wrapper } = await mountLogAt()

    expect(renderedCaptions(wrapper)).toEqual(schemaCaptions(schema.table.columns))
  })

  it('draws the table skeleton while the first load is in flight', async () => {
    stubPendingFetch()
    const { wrapper } = await mountRouted(ClientLogView, '/clients/:id/log', '/clients/7/log')
    await flushPromises()

    expect(wrapper.find('[data-testid="ui-table-skeleton"]').exists()).toBe(true)
    expect(renderedCaptions(wrapper)).toEqual(schemaCaptions(schema.table.columns))
  })
})
