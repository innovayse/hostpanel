/**
 * Tests of TopClientsView: it mounts, shows its title and data, and shows the refusal sentence with Retry when the API
 * refuses, and runs its own actions (refresh, CSV export, tile tooltips).
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import TopClientsView from '@/views/TopClientsView.vue'
import schema from '@/views/TopClientsView.schema'
import { captureDownloads } from '../helpers/captureDownloads'
import { json } from '../helpers/fetch'
import { mountView } from '../helpers/mount'
import { runReportCase } from '../helpers/reportViews'
import { runReportSchemaCases, schemaCaptionsOf } from '../helpers/reportSchema'
import { stubRouteTable } from '../helpers/routes'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportSchemaCases([{ name: 'TopClientsView', view: TopClientsView, schema }])

runReportCase({
  name: 'TopClientsView',
  view: TopClientsView,
  routes: {
    'GET /reports/top-clients-by-income': () => json([
      { clientId: 1, clientName: 'Beta', totalAmountIn: 100, totalFees: 2, totalAmountOut: 0, balance: 98, currency: 'USD' },
    ]),
  },
  expected: [...schemaCaptionsOf(schema), 'Top 10 Clients by Income', 'Beta', '$98.00'],
  retryable: true,
})

describe('TopClientsView actions', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('downloads its table as a CSV file', async () => {
    stubRouteTable({
      'GET /reports/top-clients-by-income': () => json([
        { clientId: 1, clientName: 'Beta', totalAmountIn: 100, totalFees: 2, totalAmountOut: 0, balance: 98, currency: 'USD' },
      ]),
    })
    const wrapper = await mountView(TopClientsView)
    const saved = captureDownloads()

    await wrapper.get('[data-testid="report-export"]').trigger('click')

    expect(saved).toHaveLength(1)
    expect(saved[0]?.fileName).toMatch(/\.csv$/)
  })
})

describe('TopClientsView refresh and tooltips', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('reads the report again when Refresh is pressed and explains each tile', async () => {
    const calls: string[] = []
    stubRouteTable({
      'GET /reports/top-clients-by-income': () => {
        calls.push('read')
        return json([
          { clientId: 1, clientName: 'Beta', totalAmountIn: 100, totalFees: 2, totalAmountOut: 0, balance: 98, currency: 'USD' },
        ])
      },
    })
    const wrapper = await mountView(TopClientsView)
    await flushPromises()

    await wrapper.get('[data-testid="report-refresh"]').trigger('click')
    await flushPromises()

    expect(calls).toHaveLength(2)
    expect(wrapper.find('[data-testid="report-stat-leader-tooltip"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="report-stat-total-tooltip"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="report-stat-clients-tooltip"]').exists()).toBe(true)
  })
})
