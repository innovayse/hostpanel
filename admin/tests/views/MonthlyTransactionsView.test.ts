/**
 * Tests of MonthlyTransactionsView: it mounts, shows its title and data, and shows the refusal sentence with Retry when the API
 * refuses.
 */
import { vi } from 'vitest'
import MonthlyTransactionsView from '@/views/MonthlyTransactionsView.vue'
import schema from '@/views/MonthlyTransactionsView.schema'
import { json } from '../helpers/fetch'
import { runReportCase } from '../helpers/reportViews'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportCase({
  name: 'MonthlyTransactionsView',
  view: MonthlyTransactionsView,
  schema,
  routes: {
    'GET /reports/daily-transactions': () => json({
      month: 1, year: 2024, totalAmountIn: 10, totalFees: 1, totalAmountOut: 2, currency: 'USD',
      rows: [{ date: '2024-01-02', amountIn: 10, fees: 1, amountOut: 2, balance: 7 }],
    }),
  },
  expected: ['Monthly Transactions Report for January 2024', '02/01/2024', '$7.00'],
  retryable: true,
})
