/**
 * Mount tests for NetworkIssuesView: the list loads from the address, the search, the status, type and priority
 * filters and the page are kept in the address, sent to the API and shown as removable chips, the delete confirmation
 * with its outcome, the empty state and the retry of a refused load, through the real store and transport.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import NetworkIssuesView from '@/views/NetworkIssuesView.vue'
import schema from '@/views/NetworkIssuesView.schema'
import { renderedCaptions, schemaCaptions } from '../helpers/schemaTable'
import { NetworkIssuePriority, NetworkIssueStatus, NetworkIssueType } from '@/types/networkissue'
import { applyListFilter } from '../helpers/listFilter'
import { mountRouted, mountView } from '../helpers/mount'
import { json, problem, stubFetch, callOf } from '../helpers/fetch'
import { stubPathRoutes, stubRoutes } from '../helpers/routes'
import { useToastStore } from '@/stores/toast'

/** One network issue as the list endpoint returns it. */
const issue = {
  id: 2,
  title: 'Packet loss',
  type: NetworkIssueType.Server,
  server: 'srv-1',
  priority: NetworkIssuePriority.Critical,
  status: NetworkIssueStatus.Investigating,
  startDate: '2026-10-01T08:00:00Z',
  endDate: null,
  description: 'Investigating',
  createdAt: '2026-10-01T08:00:00Z',
}

/** The one-issue list. */
const list = () => json({ items: [issue], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1 })

/**
 * Mounts the screen at an address and waits for its first load.
 *
 * @param query - The query string to open, with its leading question mark or empty.
 * @returns The wrapper and the router.
 */
const mountListAt = async (query = '') => {
  const screen = await mountRouted(NetworkIssuesView, '/support/network-issues', `/support/network-issues${query}`)
  await flushPromises()
  return screen
}

/**
 * Lists the requests the mock has seen for the list endpoint.
 *
 * @param mock - The stubbed fetch.
 * @returns The request URLs that went to the list endpoint.
 */
const listUrls = (mock: ReturnType<typeof vi.fn>): string[] =>
  mock.mock.calls.map(([url]) => url as string).filter((url) => url.startsWith('/api/network-issues?'))

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('NetworkIssuesView', () => {
  it('draws the table head of its schema columns, in order', async () => {
    stubPathRoutes([{ path: '/network-issues', body: { items: [issue], totalCount: 1 } }])

    const wrapper = await mountView(NetworkIssuesView)

    expect(renderedCaptions(wrapper)).toEqual(schemaCaptions(schema.table.columns))
  })

  it('loads every issue first and lists the issue with its type, priority and status', async () => {
    const mock = stubPathRoutes([{ path: '/network-issues', body: { items: [issue], totalCount: 1 } }])

    const wrapper = await mountView(NetworkIssuesView)
    const row = wrapper.get('[data-testid="network-issues-row-2"]')

    expect(callOf(mock).url).toBe('/api/network-issues?page=1&pageSize=20')
    expect(row.text()).toContain('Packet loss')
    expect(row.text()).toContain('Server')
    expect(row.text()).toContain('Critical')
    expect(row.text()).toContain('Investigating')
  })

  it('has no status tabs any more', async () => {
    stubPathRoutes([{ path: '/network-issues', body: { items: [issue], totalCount: 1 } }])

    const wrapper = await mountView(NetworkIssuesView)

    expect(wrapper.find('[data-testid="network-issues-tab-Resolved"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="network-issues-filter"]').exists()).toBe(true)
  })

  it('puts the typed search in the address and sends it after the pause', async () => {
    const mock = stubRoutes({ 'GET /api/network-issues': list })
    const { wrapper, router } = await mountListAt()

    await wrapper.get('[data-testid="network-issues-search"]').setValue('srv-1')
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'srv-1' })
    expect(listUrls(mock).at(-1)).toBe('/api/network-issues?page=1&pageSize=20&search=srv-1')
  })

  it('sends the status, type and priority picked in the filter dialog on Apply and shows them as chips', async () => {
    const mock = stubRoutes({ 'GET /api/network-issues': list })
    const { wrapper, router } = await mountListAt()

    await applyListFilter(
      wrapper,
      {
        'network-issues-filter-status': 'Resolved',
        'network-issues-filter-type': 'Other',
        'network-issues-filter-priority': 'High',
      },
      'network-issues-filter',
    )

    expect(router.currentRoute.value.query).toEqual({ status: 'Resolved', type: 'Other', priority: 'High' })
    expect(listUrls(mock).at(-1)).toBe('/api/network-issues?page=1&pageSize=20&status=Resolved&type=Other&priority=High')
    expect(wrapper.get('[data-testid="filter-chip-status"]').text()).toContain('Resolved')
    expect(wrapper.get('[data-testid="filter-chip-type"]').text()).toContain('Other')
    expect(wrapper.get('[data-testid="filter-chip-priority"]').text()).toContain('High')
  })

  it('restores the search, the filters and the page from the address and sends them together', async () => {
    const mock = stubRoutes({ 'GET /api/network-issues': list })
    const { wrapper } = await mountListAt('?search=loss&status=Scheduled&type=Server&priority=Low&page=2')

    expect(listUrls(mock)[0]).toBe('/api/network-issues?page=2&pageSize=20&search=loss&status=Scheduled&type=Server&priority=Low')
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('3')
  })

  it('does not send a filter the address holds that is not a known value', async () => {
    const mock = stubRoutes({ 'GET /api/network-issues': list })
    const { wrapper } = await mountListAt('?status=Archived&type=Sideways&priority=Urgent')

    expect(listUrls(mock)[0]).toBe('/api/network-issues?page=1&pageSize=20')
    expect(wrapper.find('[data-testid="filter-chips"]').exists()).toBe(false)
  })

  it('removes a filter from the address and reloads when its chip is removed', async () => {
    const mock = stubRoutes({ 'GET /api/network-issues': list })
    const { wrapper, router } = await mountListAt('?status=Resolved&priority=High')

    await wrapper.get('[data-testid="filter-chip-remove-status"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ priority: 'High' })
    expect(listUrls(mock).at(-1)).toBe('/api/network-issues?page=1&pageSize=20&priority=High')
  })

  it('keeps the chosen page in the address and loads it', async () => {
    const mock = stubRoutes({
      'GET /api/network-issues': () => json({ items: [issue], pageNumber: 1, pageSize: 20, totalCount: 60, totalPages: 3 }),
    })
    const { wrapper, router } = await mountListAt()

    await wrapper.get('[data-testid="network-issues-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '2' })
    expect(listUrls(mock).at(-1)).toBe('/api/network-issues?page=2&pageSize=20')
  })

  it('deletes after confirmation', async () => {
    const mock = stubPathRoutes([{ path: '/network-issues', body: { items: [issue], totalCount: 1 } }])
    const wrapper = await mountView(NetworkIssuesView)

    await wrapper.get('[data-testid="network-issues-2-actions"]').trigger('click')

    await wrapper.get('[data-testid="network-issues-2-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(callOf(mock, 1)).toMatchObject({ url: '/api/network-issues/2', method: 'DELETE' })
    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['Record deleted.'])
  })

  it('steps back one page when a delete empties the last page', async () => {
    let deleted = false
    stubRoutes({
      'DELETE /api/network-issues/2': () => {
        deleted = true
        return new Response(null, { status: 204 })
      },
      'GET /api/network-issues?page=2': () =>
        json({ items: deleted ? [] : [issue], pageNumber: 2, pageSize: 20, totalCount: deleted ? 20 : 21, totalPages: deleted ? 1 : 2 }),
      'GET /api/network-issues?page=1': list,
    })
    const { wrapper, router } = await mountListAt('?page=2')

    await wrapper.get('[data-testid="network-issues-2-actions"]').trigger('click')
    await wrapper.get('[data-testid="network-issues-2-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query.page ?? '1').toBe('1')
  })

  it('shows the API sentence when the delete is refused', async () => {
    stubRoutes({
      'DELETE /api/network-issues/2': () => problem('The issue is locked.', 409),
      'GET /api/network-issues': () => json({ items: [issue], totalCount: 1 }),
    })
    const wrapper = await mountView(NetworkIssuesView)

    await wrapper.get('[data-testid="network-issues-2-actions"]').trigger('click')

    await wrapper.get('[data-testid="network-issues-2-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="network-issues-feedback"]').text()).toBe('The issue is locked.')
    expect(wrapper.find('[data-testid="network-issues-row-2"]').exists()).toBe(true)
  })

  it('does not delete when the confirmation is dismissed', async () => {
    const mock = stubRoutes({ 'GET /api/network-issues': () => json({ items: [issue], totalCount: 1 }) })
    const wrapper = await mountView(NetworkIssuesView)

    await wrapper.get('[data-testid="network-issues-2-actions"]').trigger('click')

    await wrapper.get('[data-testid="network-issues-2-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(false)
  })

  it('shows the empty state when there are no issues', async () => {
    stubPathRoutes([{ path: '/network-issues', body: { items: [], totalCount: 0 } }])

    const wrapper = await mountView(NetworkIssuesView)

    expect(wrapper.get('[data-testid="network-issues-table"]').text()).toContain('No Records Found')
  })

  it('shows the API sentence when the list is refused', async () => {
    stubFetch(problem('Not allowed.', 403))

    const wrapper = await mountView(NetworkIssuesView)

    expect(wrapper.get('[data-testid="network-issues-error"]').text()).toBe('Not allowed.')
  })

  it('loads again from the retry of a refused list', async () => {
    let calls = 0
    stubRoutes({
      'GET /api/network-issues': () => {
        calls += 1
        return calls === 1 ? problem('Not allowed.', 403) : json({ items: [issue], totalCount: 1 })
      },
    })
    const wrapper = await mountView(NetworkIssuesView)

    await wrapper.get('[data-testid="network-issues-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="network-issues-row-2"]').exists()).toBe(true)
  })
})
