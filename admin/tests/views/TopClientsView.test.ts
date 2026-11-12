/**
 * Tests of TopClientsView: it mounts, shows its title and data, and shows the refusal sentence with Retry when the API
 * refuses, and runs its own actions.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import TopClientsView from '@/views/TopClientsView.vue'
import { captureDownloads } from '../helpers/captureDownloads'
import { json } from '../helpers/fetch'
import { mountView } from '../helpers/mount'
import { runReportCase } from '../helpers/reportViews'
import { stubRouteTable } from '../helpers/routes'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportCase({
  name: 'TopClientsView',
  view: TopClientsView,
  routes: {
    'GET /reports/top-clients-by-income': () => json([
      { clientId: 1, clientName: 'Beta', totalAmountIn: 100, totalFees: 2, totalAmountOut: 0, balance: 98, currency: 'USD' },
    ]),
  },
  expected: ['Top 10 Clients by Income', 'Beta', '$98.00'],
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
