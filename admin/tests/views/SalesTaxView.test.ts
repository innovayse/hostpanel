/**
 * Tests of SalesTaxView: it mounts, shows its title, the totals per currency and the rows in their
 * own currency, and shows the refusal sentence with Retry when the API refuses.
 */
import { vi } from 'vitest'
import SalesTaxView from '@/views/SalesTaxView.vue'
import schema from '@/views/SalesTaxView.schema'
import { json } from '../helpers/fetch'
import { runReportCase } from '../helpers/reportViews'
import { runReportSchemaCases, schemaCaptionsOf } from '../helpers/reportSchema'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportSchemaCases([{ name: 'SalesTaxView', view: SalesTaxView, schema }])

runReportCase({
  name: 'SalesTaxView',
  view: SalesTaxView,
  routes: {
    'GET /reports/sales-tax': () => json({
      totals: [{ currency: 'EUR', totalInvoices: 1, totalInvoiced: 11, taxLiability: 1 }],
      rows: {
        items: [{ id: 3, clientName: 'Beta', invoiceDate: '2024-01-01', datePaid: '2024-01-02', currency: 'EUR', subTotal: 10, tax: 1, credit: 0, total: 11 }],
        pageNumber: 1, pageSize: 50, totalCount: 1, totalPages: 1,
      },
    }),
  },
  expected: [...schemaCaptionsOf(schema), 'Sales Tax Liability', 'Beta', '€11.00', 'EUR'],
  retryable: true,
})
