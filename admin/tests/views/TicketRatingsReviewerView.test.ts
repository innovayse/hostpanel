/**
 * Tests of TicketRatingsReviewerView: it mounts, shows its title and data, and shows the refusal sentence with Retry when the API
 * refuses.
 */
import { vi } from 'vitest'
import TicketRatingsReviewerView from '@/views/TicketRatingsReviewerView.vue'
import schema from '@/views/TicketRatingsReviewerView.schema'
import { json } from '../helpers/fetch'
import { runReportCase } from '../helpers/reportViews'
import { runReportSchemaCases, schemaCaptionsOf } from '../helpers/reportSchema'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportSchemaCases([{ name: 'TicketRatingsReviewerView', view: TicketRatingsReviewerView, schema }])

runReportCase({
  name: 'TicketRatingsReviewerView',
  view: TicketRatingsReviewerView,
  routes: {
    'GET /reports/ticket-ratings-reviewer': () => json({
      rows: [{ ticketId: 5, date: '2024-02-01T12:00:00Z', message: 'Thanks a lot', adminName: 'Ann', rating: 9 }],
    }),
  },
  expected: [...schemaCaptionsOf(schema), 'Support Ticket Ratings Reviewer', 'Thanks a lot', '#5'],
  retryable: true,
})
