/**
 * Tests of SupportTicketsReportView: it mounts, shows its title and data, and shows the refusal sentence with Retry when the API
 * refuses.
 */
import { vi } from 'vitest'
import SupportTicketsReportView from '@/views/SupportTicketsReportView.vue'
import schema from '@/views/SupportTicketsReportView.schema'
import { json } from '../helpers/fetch'
import { runReportCase } from '../helpers/reportViews'
import { runReportSchemaCases, schemaCaptionsOf } from '../helpers/reportSchema'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportSchemaCases([{ name: 'SupportTicketsReportView', view: SupportTicketsReportView, schema }])

runReportCase({
  name: 'SupportTicketsReportView',
  view: SupportTicketsReportView,
  routes: {
    'GET /reports/support-ticket-replies': () => json({
      month: 1, year: 2024, daysInMonth: 3,
      rows: [{ adminName: 'Ann', dailyCounts: [1, 0, 2], total: 3 }],
    }),
  },
  expected: [...schemaCaptionsOf(schema), 'Support Ticket Replies for January 2024', 'Ann', 'Average per day', '1.0', 'Day 3: 2 replies'],
  retryable: true,
})
