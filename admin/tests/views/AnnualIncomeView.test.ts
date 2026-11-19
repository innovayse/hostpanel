/**
 * Tests of AnnualIncomeView: it mounts, shows its title and data, and shows the refusal sentence with Retry when the API
 * refuses.
 */
import { vi } from 'vitest'
import AnnualIncomeView from '@/views/AnnualIncomeView.vue'
import schema from '@/views/AnnualIncomeView.schema'
import { json } from '../helpers/fetch'
import { runReportCase } from '../helpers/reportViews'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportCase({
  name: 'AnnualIncomeView',
  view: AnnualIncomeView,
  schema,
  routes: { 'GET /reports/annual-income': () => json([{ month: 'January', amount: 12, currency: 'USD' }]) },
  expected: ['Annual Income Report', 'January', '$12.00'],
  retryable: true,
})
