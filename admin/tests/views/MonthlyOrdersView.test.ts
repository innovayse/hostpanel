/**
 * Tests of MonthlyOrdersView: it mounts, shows its title and data, and shows the refusal sentence with Retry when the API
 * refuses.
 */
import { vi } from 'vitest'
import MonthlyOrdersView from '@/views/MonthlyOrdersView.vue'
import schema from '@/views/MonthlyOrdersView.schema'
import { json } from '../helpers/fetch'
import { runReportCase } from '../helpers/reportViews'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

/** A product group of the report. */
const productGroup = { groupName: 'Hosting', groupUnitsSold: 2, products: [{ productId: 1, productName: 'Basic', unitsSold: 2 }] }

runReportCase({
  name: 'MonthlyOrdersView',
  view: MonthlyOrdersView,
  schema,
  routes: {
    'GET /reports/monthly-orders': () => json({
      month: 1, year: 2024, totalUnitsSold: 2, totalValue: 20, currency: 'USD',
      groups: [{ ...productGroup, groupValue: 20, products: [{ ...productGroup.products[0], value: 20 }] }],
    }),
  },
  expected: ['Sales by Product for January 2024', 'Basic', '$20.00'],
  retryable: true,
})
