/**
 * Tests of IncomeByProductView: it mounts, shows its title and data, and shows the refusal sentence with Retry when the API
 * refuses.
 */
import { vi } from 'vitest'
import IncomeByProductView from '@/views/IncomeByProductView.vue'
import schema from '@/views/IncomeByProductView.schema'
import { json } from '../helpers/fetch'
import { runReportCase } from '../helpers/reportViews'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

/** A product group of the report. */
const productGroup = { groupName: 'Hosting', groupUnitsSold: 2, products: [{ productId: 1, productName: 'Basic', unitsSold: 2 }] }

runReportCase({
  name: 'IncomeByProductView',
  view: IncomeByProductView,
  schema,
  routes: {
    'GET /reports/income-by-product-grouped': () => json({
      month: 1, year: 2024, totalUnitsSold: 2, totalIncome: 20, currency: 'USD',
      groups: [{ ...productGroup, groupIncome: 20, products: [{ ...productGroup.products[0], totalIncome: 20 }] }],
    }),
  },
  expected: ['Income by Product for January 2024', 'Basic', '$20.00'],
  retryable: true,
})
