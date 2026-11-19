/**
 * Tests of TicketFeedbackCommentsView: it mounts, shows its title and data, and shows the refusal sentence with Retry when the API
 * refuses.
 */
import { vi } from 'vitest'
import TicketFeedbackCommentsView from '@/views/TicketFeedbackCommentsView.vue'
import schema from '@/views/TicketFeedbackCommentsView.schema'
import { json } from '../helpers/fetch'
import { runReportCase } from '../helpers/reportViews'
import { runReportSchemaCases, schemaCaptionsOf } from '../helpers/reportSchema'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportSchemaCases([{ name: 'TicketFeedbackCommentsView', view: TicketFeedbackCommentsView, schema }])

runReportCase({
  name: 'TicketFeedbackCommentsView',
  view: TicketFeedbackCommentsView,
  routes: {
    'GET /reports/ticket-feedback-comments': () => json({
      rows: [{
        ticketId: 5, staffName: 'Ann', subject: 'Help', feedbackComment: 'Great service', rating: 9,
        feedbackLeftBy: 'Beta', feedbackAt: '2024-02-01T12:00:00Z',
      }],
    }),
  },
  expected: [...schemaCaptionsOf(schema), 'Ticket Feedback Comments', 'Great service', 'Beta', '01/02/2024'],
  retryable: true,
})
