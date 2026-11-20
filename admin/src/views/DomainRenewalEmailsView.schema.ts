/**
 * Skeleton schema of the domain renewal emails report: the client, registrar, domain and date range filters and the
 * table of the reminders that were sent.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiAlign, UiColumnSize } from '@/utils/ui/table'

/** What the domain renewal emails report is made of. */
const schema: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  header: { title: 'reports.domainRenewalEmails.domainRenewalReminderEmails', subtitle: 'reports.domainRenewalEmails.thisReportCanBeUsed' },
  filters: [
    { key: 'client', label: 'shared.client' },
    { key: 'registrar', label: 'shared.registrar' },
    { key: 'domain', label: 'shared.domain' },
    { key: 'dateRange', label: 'shared.dateRange' },
  ],
  table: {
    columns: [
      { key: 'clientName', label: 'shared.client', size: UiColumnSize.Grow },
      { key: 'domain', label: 'shared.domain' },
      { key: 'sentAt', label: 'reports.domainRenewalEmails.dateSent' },
      { key: 'reminder', label: 'reports.domainRenewalEmails.reminder' },
      { key: 'recipients', label: 'reports.domainRenewalEmails.recipients' },
      { key: 'sent', label: 'reports.domainRenewalEmails.sent', align: UiAlign.Center },
    ],
  },
  emptyText: 'shared.noDataFoundForThis2',
}

export default schema
