/**
 * Tests of PromotionsView: it mounts, shows its title and data, and shows the refusal sentence with Retry when the API
 * refuses.
 */
import { vi } from 'vitest'
import PromotionsView from '@/views/PromotionsView.vue'
import schema from '@/views/PromotionsView.schema'
import { json } from '../helpers/fetch'
import { runReportCase } from '../helpers/reportViews'
import { runReportSchemaCases, schemaCaptionsOf } from '../helpers/reportSchema'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportSchemaCases([{ name: 'PromotionsView', view: PromotionsView, schema }])

runReportCase({
  name: 'PromotionsView',
  view: PromotionsView,
  routes: {
    'GET /reports/promotions': () => json({
      currency: 'USD',
      rows: [{ code: 'SPRING25', services: 4, activeServices: 3, monthlyRevenue: 42 }],
    }),
  },
  expected: [...schemaCaptionsOf(schema), 'Promotions', 'SPRING25', '$42.00'],
  retryable: true,
})
