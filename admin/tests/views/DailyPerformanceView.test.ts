/**
 * Tests of DailyPerformanceView: it mounts, shows its title and data, and shows the refusal sentence with Retry when the API
 * refuses.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import DailyPerformanceView from '@/views/DailyPerformanceView.vue'
import schema from '@/views/DailyPerformanceView.schema'
import { json } from '../helpers/fetch'
import { mountView } from '../helpers/mount'
import { runReportCase } from '../helpers/reportViews'
import { stubRouteTable } from '../helpers/routes'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportCase({
  name: 'DailyPerformanceView',
  view: DailyPerformanceView,
  schema,
  routes: {
    'GET /reports/daily-performance': () => json([{
      date: '2024-01-01', completedOrders: 1, newInvoices: 2, paidInvoices: 3, openedTickets: 4, ticketReplies: 5, cancellationRequests: 6,
    }]),
  },
  expected: ['Daily Performance', '01/01/2024', 'Cancellation Requests'],
  retryable: true,
})

describe('DailyPerformanceView actions and tooltips', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('offers a refresh button and explains each tile with a tooltip', async () => {
    stubRouteTable({ 'GET /reports/daily-performance': () => json([]) })
    const wrapper = await mountView(DailyPerformanceView)

    expect(wrapper.find('[data-testid="report-refresh"]').exists()).toBe(true)
    for (const stat of schema.stats ?? []) {
      expect(wrapper.find(`[data-testid="report-stat-${stat.key}-tooltip"]`).exists()).toBe(true)
    }
  })
})
