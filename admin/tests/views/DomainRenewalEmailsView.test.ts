/**
 * Tests of DomainRenewalEmailsView: it mounts, shows its title and data, and shows the refusal sentence when the API
 * refuses.
 */
import { vi } from 'vitest'
import { DomainReminderType } from '@/types/domain'
import DomainRenewalEmailsView from '@/views/DomainRenewalEmailsView.vue'
import schema from '@/views/DomainRenewalEmailsView.schema'
import { json } from '../helpers/fetch'
import { runReportCase } from '../helpers/reportViews'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

runReportCase({
  name: 'DomainRenewalEmailsView',
  view: DomainRenewalEmailsView,
  schema,
  routes: {
    'GET /reports/domain-renewal-emails': () => json({
      rows: [{ clientName: 'Beta', domain: 'beta.test', sentAt: '2024-03-05T12:00:00Z', reminder: DomainReminderType.ThirtyDaysBeforeExpiry, recipients: 'a@b.test' }],
    }),
  },
  expected: ['Domain Renewal Reminder Emails', 'beta.test', '05/03/2024', '30 days before expiry', 'a@b.test'],
  retryable: true,
})
