/**
 * Tests of AgingInvoicesView: it mounts, shows its title and data, and shows the refusal sentence when the API
 * refuses.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ReportAgingBucket } from '@/types/reportaging'
import AgingInvoicesView from '@/views/AgingInvoicesView.vue'
import schema from '@/views/AgingInvoicesView.schema'
import { json } from '../helpers/fetch'
import { mountView } from '../helpers/mount'
import { runReportCase } from '../helpers/reportViews'
import { stubRouteTable } from '../helpers/routes'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportCase({
  name: 'AgingInvoicesView',
  view: AgingInvoicesView,
  schema,
  dataColumns: true,
  routes: {
    'GET /reports/aging-invoices-summary': () => json({
      periods: [{ period: ReportAgingBucket.Days0To30, amountsByCurrency: { USD: 10 } }],
      totals: { USD: 10 },
      currencies: ['USD'],
    }),
  },
  expected: ['Aging Invoices', '0 - 30', '$10.00'],
  retryable: true,
})

describe('AgingInvoicesView columns', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('replaces the schema amount column with one per currency once the report is read', async () => {
    stubRouteTable({
      'GET /reports/aging-invoices-summary': () => json({
        periods: [{ period: ReportAgingBucket.Days0To30, amountsByCurrency: { USD: 10, EUR: 4 } }],
        totals: { USD: 10, EUR: 4 },
        currencies: ['USD', 'EUR'],
      }),
    })
    const wrapper = await mountView(AgingInvoicesView)

    expect(wrapper.findAll('th').map(header => header.text())).toEqual(['Period', 'USD Amount', 'EUR Amount'])
  })

  it('draws the pie and the table, with the schema columns, before the first answer', async () => {
    stubRouteTable({ 'GET /reports/aging-invoices-summary': () => new Promise<Response>(() => undefined) })
    const wrapper = await mountView(AgingInvoicesView)

    expect(wrapper.find('[data-testid="report-chart"]').exists()).toBe(true)
    expect(wrapper.findAll('th').map(header => header.text())).toEqual(['Period', 'Amount'])
  })
})
