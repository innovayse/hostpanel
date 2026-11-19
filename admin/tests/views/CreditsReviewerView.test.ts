/**
 * Tests of CreditsReviewerView: it mounts, shows its title, the totals per currency and each credit
 * in its own currency, and shows the refusal sentence with Retry when the API refuses.
 */
import { vi } from 'vitest'
import CreditsReviewerView from '@/views/CreditsReviewerView.vue'
import schema from '@/views/CreditsReviewerView.schema'
import { json } from '../helpers/fetch'
import { runReportCase } from '../helpers/reportViews'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportCase({
  name: 'CreditsReviewerView',
  view: CreditsReviewerView,
  schema,
  routes: {
    'GET /reports/credits-reviewer': () => json({
      totals: [{ currency: 'AMD', totalCount: 1, totalAmount: 5000 }],
      rows: {
        items: [{ id: 1, clientId: 2, clientName: 'Beta', date: '2024-01-01', description: 'Gift', currency: 'AMD', amount: 5000 }],
        pageNumber: 1, pageSize: 50, totalCount: 1, totalPages: 1,
      },
    }),
  },
  expected: ['Credits Reviewer', 'Beta', 'Gift', 'AMD'],
  retryable: true,
})
