/**
 * Tests of ServerRevenueForecastsView: it mounts, shows its title and data, and shows the refusal sentence with Retry when
 * the API refuses.
 */
import { vi } from 'vitest'
import ServerRevenueForecastsView from '@/views/ServerRevenueForecastsView.vue'
import schema from '@/views/ServerRevenueForecastsView.schema'
import { json } from '../helpers/fetch'
import { runReportCase } from '../helpers/reportViews'
import { runReportSchemaCases, schemaCaptionsOf } from '../helpers/reportSchema'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportSchemaCases([{ name: 'ServerRevenueForecastsView', view: ServerRevenueForecastsView, schema }])

runReportCase({
  name: 'ServerRevenueForecastsView',
  view: ServerRevenueForecastsView,
  routes: {
    'GET /reports/server-revenue-forecasts': () => json({
      currency: 'USD',
      rows: [{ serverId: 1, serverName: 'Alpha', hostname: 'alpha.example.test', activeServices: 5, monthlyRevenue: 100, annualRevenue: 1200 }],
    }),
  },
  expected: [...schemaCaptionsOf(schema), 'Server Revenue Forecasts', 'Alpha', 'alpha.example.test', '$100.00', '$1,200.00'],
  retryable: true,
})
