/**
 * Tests of NewCustomersView: it mounts, shows its title and data, and shows the refusal sentence with Retry when the API
 * refuses.
 */
import { vi } from 'vitest'
import NewCustomersView from '@/views/NewCustomersView.vue'
import schema from '@/views/NewCustomersView.schema'
import { json } from '../helpers/fetch'
import { runReportCase } from '../helpers/reportViews'
import { runReportSchemaCases, schemaCaptionsOf } from '../helpers/reportSchema'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportSchemaCases([{ name: 'NewCustomersView', view: NewCustomersView, schema }])

runReportCase({
  name: 'NewCustomersView',
  view: NewCustomersView,
  routes: {
    'GET /reports/new-customers': () => json([{ month: 'Jan', newSignups: 3, ordersPlaced: 2, ordersCompleted: 1, conversionRate: 0.5 }]),
  },
  expected: [...schemaCaptionsOf(schema), 'New Customers', 'Jan', 'New Signups'],
  retryable: true,
})
