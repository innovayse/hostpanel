/**
 * Tests of TicketTagsView: it mounts, shows its title and data, and shows the refusal sentence with Retry when the API
 * refuses.
 */
import { vi } from 'vitest'
import TicketTagsView from '@/views/TicketTagsView.vue'
import schema from '@/views/TicketTagsView.schema'
import { json } from '../helpers/fetch'
import { runReportCase } from '../helpers/reportViews'
import { runReportSchemaCases, schemaCaptionsOf } from '../helpers/reportSchema'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportSchemaCases([{ name: 'TicketTagsView', view: TicketTagsView, schema }])

runReportCase({
  name: 'TicketTagsView',
  view: TicketTagsView,
  routes: { 'GET /reports/ticket-tags': () => json({ rows: [{ tag: 'billing', count: 4 }] }) },
  expected: [...schemaCaptionsOf(schema), 'Ticket Tags Overview', 'billing'],
  retryable: true,
})
