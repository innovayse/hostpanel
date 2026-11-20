/**
 * Tests of OssReportView: it mounts, names the country from its code in the reader's language, shows
 * the quarter's bounds and each amount in its row currency, and shows the refusal sentence with
 * Retry when the API refuses.
 */
import { vi } from 'vitest'
import OssReportView from '@/views/OssReportView.vue'
import schema from '@/views/OssReportView.schema'
import { json } from '../helpers/fetch'
import { runReportCase } from '../helpers/reportViews'
import { runReportSchemaCases, schemaCaptionsOf } from '../helpers/reportSchema'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportSchemaCases([{ name: 'OssReportView', view: OssReportView, schema }])

runReportCase({
  name: 'OssReportView',
  view: OssReportView,
  routes: {
    'GET /reports/oss': () => json({
      periodStart: '2024-01-01',
      periodEnd: '2024-03-31',
      rows: [{ countryCode: 'DE', vatRate: 19, numberOfInvoices: 2, totalExclVat: 100, totalVatCollected: 19, currency: 'EUR' }],
    }),
  },
  expected: [...schemaCaptionsOf(schema), 'OSS Report (One Stop Shop)', 'For the period 01/01/2024 to 31/03/2024', 'Germany', '19%', '€100.00', '€19.00'],
  retryable: true,
})
