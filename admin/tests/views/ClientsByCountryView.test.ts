/**
 * Tests of ClientsByCountryView: it mounts, shows its title and data, and shows the refusal sentence with Retry when the API
 * refuses.
 */
import { vi } from 'vitest'
import ClientsByCountryView from '@/views/ClientsByCountryView.vue'
import schema from '@/views/ClientsByCountryView.schema'
import { json } from '../helpers/fetch'
import { runReportCase } from '../helpers/reportViews'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportCase({
  name: 'ClientsByCountryView',
  view: ClientsByCountryView,
  schema,
  routes: {
    'GET /reports/clients-by-country': () => json([{ country: 'AM', clientCount: 3, totalRevenue: 1 }]),
  },
  expected: ['Clients by Country', 'Armenia', 'Total Clients: 3'],
  retryable: true,
})
