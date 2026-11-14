/**
 * Mount tests for DashboardView: the greeting and period toggle, the four KPI tiles with the formatted numbers the
 * API sent (placeholders while they load), a refusal showing the API's sentence, the chart cards filled from the
 * chart series, and the period toggle reloading every chart and the revenue tile.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import { flushPromises } from '@vue/test-utils'
import DashboardView from '@/views/DashboardView.vue'
import { dashboardCharts } from '../helpers/dashboardFixtures'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'
import { json, problem, stubFetch } from '../helpers/fetch'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

/** Statistics as the dashboard endpoint returns them. */
const stats = {
  totalRevenue: 12500,
  monthlyRevenue: 900,
  activeServices: 42,
  overdueInvoices: 3,
  openTickets: 7,
  totalClients: 120,
  currency: 'USD',
}

/**
 * One page of a list endpoint holding the given items.
 *
 * @param items - The page items.
 * @returns The page body.
 */
const pageOf = (items: unknown[]) => ({ items, pageNumber: 1, pageSize: 5, totalCount: items.length, totalPages: 1 })

/**
 * Answers each request of the dashboard by its URL: the statistics, the chart series, and a list per panel.
 *
 * @param url - Requested URL.
 * @returns The response for it.
 */
const answer = (url: string): Response => {
  if (url.includes('/dashboard/stats')) {
    return json(stats)
  }
  if (url.includes('/dashboard/charts')) {
    return json(dashboardCharts())
  }
  if (url.includes('/orders')) {
    return json(pageOf([{ id: 4, orderNumber: 'ORD-0004', clientName: 'Bob', status: 'Pending', total: 25, currency: 'USD' }]))
  }
  if (url.includes('/billing')) {
    return json(pageOf([{ id: 11, clientName: 'Ada', status: 'Overdue', total: 50, currency: 'USD', dueDate: '2026-10-01T00:00:00Z' }]))
  }
  if (url.includes('/admin/servers')) {
    return json([{ id: 1, name: 'alpha', accountsCount: 50, limits: { maxAccounts: 100 } }])
  }
  return json(pageOf([]))
}

describe('DashboardView', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('renders four tiles with money grouped and counts plain', async () => {
    vi.stubGlobal('fetch', vi.fn((url: string) => Promise.resolve(answer(url))))
    const { wrapper } = await mountRouted(DashboardView, '/', '/')
    await flushPromises()

    expect(wrapper.get('[data-testid="dashboard-stat-periodRevenue"]').text()).toContain('$450')
    expect(wrapper.get('[data-testid="dashboard-stat-periodRevenue"]').text()).toContain('+50%')
    expect(wrapper.get('[data-testid="dashboard-stat-activeServices"]').text()).toContain('42')
    expect(wrapper.get('[data-testid="dashboard-stat-overdueInvoices"]').text()).toContain('3')
    expect(wrapper.get('[data-testid="dashboard-stat-openTickets"]').text()).toContain('7')
    expect(wrapper.find('[data-testid="dashboard-stat-totalRevenue"]').exists()).toBe(false)
  })

  it('greets and offers the 30 and 90 day toggle', async () => {
    stubRoutes({ 'GET /api/admin/dashboard/stats': () => json(stats) })
    const { wrapper } = await mountRouted(DashboardView, '/', '/')
    await flushPromises()

    expect(wrapper.get('[data-testid="dashboard-header"]').text()).toMatch(/Good (morning|afternoon|evening)/)
    expect(wrapper.get('[data-testid="dashboard-period-30"]').attributes('aria-checked')).toBe('true')
  })

  it('keeps the tiles in place with placeholders until the statistics arrive', async () => {
    stubRoutes({ 'GET /api/admin/dashboard/stats': () => json(stats) })
    const { wrapper } = await mountRouted(DashboardView, '/', '/')
    await nextTick()

    expect(wrapper.find('[data-testid="dashboard-stats"]').exists()).toBe(true)
    expect(wrapper.findAll('[data-testid="ui-stat-tile-skeleton"]')).toHaveLength(4)

    await flushPromises()
    expect(wrapper.findAll('[data-testid="ui-stat-tile-skeleton"]')).toHaveLength(1)
  })

  it('shows the API sentence when the statistics are refused', async () => {
    stubRoutes({ 'GET /api/admin/dashboard/stats': () => problem('Not allowed.', 403) })
    const { wrapper } = await mountRouted(DashboardView, '/', '/')
    await flushPromises()

    expect(wrapper.get('[data-testid="dashboard-error"]').text()).toContain('Not allowed.')
    expect(wrapper.find('[data-testid="dashboard-stats"]').exists()).toBe(false)
  })

  it('fills the chart, attention, capacity and invoice panels from the series and the lists', async () => {
    vi.stubGlobal('fetch', vi.fn((url: string) => Promise.resolve(answer(url))))
    const { wrapper } = await mountRouted(DashboardView, '/', '/')
    await flushPromises()

    expect(wrapper.get('[data-testid="dashboard-revenue-total"]').text()).toContain('$450')
    expect(wrapper.get('[data-testid="dashboard-growth-orders"]').text()).toContain('3')
    expect(wrapper.get('[data-testid="dashboard-invoice-status-Paid"]').text()).toContain('7 · $700')
    expect(wrapper.find('[data-testid="dashboard-tickets"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="dashboard-attention-overdueInvoice-count"]').text()).toBe('1')
    expect(wrapper.get('[data-testid="dashboard-attention-pendingOrder-count"]').text()).toBe('1')
    expect(wrapper.find('[data-testid="dashboard-server-load-1"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="dashboard-invoice-11"]').attributes('href')).toBe('/billing/invoices/11/view')
    expect(wrapper.find('[data-testid="dashboard-overview-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="dashboard-charts-error"]').exists()).toBe(false)
  })

  it('reloads the charts and the revenue tile for the period the toggle chooses', async () => {
    const mock = vi.fn((url: string) => Promise.resolve(answer(url)))
    vi.stubGlobal('fetch', mock)
    const { wrapper } = await mountRouted(DashboardView, '/', '/')
    await flushPromises()
    expect(mock.mock.calls.map((call) => String(call[0])).some((url) => url.includes('/dashboard/charts?period=Days30'))).toBe(true)

    await wrapper.get('[data-testid="dashboard-period-90"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="dashboard-period-90"]').attributes('aria-checked')).toBe('true')
    expect(mock.mock.calls.map((call) => String(call[0])).some((url) => url.includes('/dashboard/charts?period=Days90'))).toBe(true)
    expect(wrapper.get('[data-testid="dashboard-stat-periodRevenue"]').text()).toContain('Revenue, 90 days')
  })

  it('shows one alert when the chart series are refused and still shows the tiles', async () => {
    vi.stubGlobal('fetch', vi.fn((url: string) => Promise.resolve(url.includes('/dashboard/charts') ? problem('No charts.', 403) : answer(url))))
    const { wrapper } = await mountRouted(DashboardView, '/', '/')
    await flushPromises()

    expect(wrapper.get('[data-testid="dashboard-charts-error"]').text()).toContain('No charts.')
    expect(wrapper.find('[data-testid="dashboard-stats"]').exists()).toBe(true)
  })

  it('shows one alert when a panel list is refused and still shows the tiles', async () => {
    stubRoutes({ 'GET /api/admin/dashboard/stats': () => json(stats) })
    const { wrapper } = await mountRouted(DashboardView, '/', '/')
    await flushPromises()

    expect(wrapper.find('[data-testid="dashboard-overview-error"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="dashboard-stats"]').exists()).toBe(true)
  })

  it('loads again from the Retry of a refused load', async () => {
    stubFetch(problem('Not allowed.', 403))
    const wrapper = (await mountRouted(DashboardView, '/', '/')).wrapper
    await flushPromises()
    const fetchMock = vi.mocked(fetch)
    const before = fetchMock.mock.calls.length

    await wrapper.get('[data-testid="dashboard-error-retry"]').trigger('click')
    await flushPromises()

    expect(fetchMock.mock.calls.length).toBeGreaterThan(before)
  })
})
