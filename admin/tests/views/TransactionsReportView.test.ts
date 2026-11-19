/**
 * Tests of TransactionsReportView: it mounts, shows its title and data, and shows the refusal sentence when the API
 * refuses.
 */
import { vi } from 'vitest'
import TransactionsReportView from '@/views/TransactionsReportView.vue'
import schema from '@/views/TransactionsReportView.schema'
import { json } from '../helpers/fetch'
import { runReportCase } from '../helpers/reportViews'
import { runReportSchemaCases } from '../helpers/reportSchema'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportSchemaCases([{ name: 'TransactionsReportView', view: TransactionsReportView, schema }])

runReportCase({
  name: 'TransactionsReportView',
  view: TransactionsReportView,
  routes: { 'GET /reports/transactions': () => json({ items: [], pageNumber: 1, pageSize: 50, totalCount: 0, totalPages: 0 }) },
  expected: ['Transactions', '0 transaction(s) found', 'No transactions found.'],
})
