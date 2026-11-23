/**
 * Skeleton schema of the domains report: the print action, the field picker and the three date ranges, and the table
 * with the columns shown before the operator chooses others (those keys are also the screen's default columns), with
 * its pager.
 */
import { REPORT_FIELDS_KEY } from '@/lib/reportsSchema'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { ReportAction } from '@/utils/ui/reportParts'
import { UiColumnKind } from '@/utils/ui/table'

/** What the domains report is made of. */
const schema: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  actions: [ReportAction.Print],
  header: { title: 'shared.domains', subtitle: 'reports.domainsReport.thisReportCanBeUsed' },
  filters: [
    { key: REPORT_FIELDS_KEY, label: 'shared.fieldsToInclude' },
    { key: 'registered', label: 'reports.domainsReport.registrationDateRange' },
    { key: 'nextDue', label: 'reports.domainsReport.nextDueDateRange' },
    { key: 'expires', label: 'reports.domainsReport.expiryDateRange' },
  ],
  table: {
    columns: [
      { key: 'id', label: 'shared.id', kind: UiColumnKind.Id },
      { key: 'clientName', label: 'shared.clientName' },
      { key: 'domainName', label: 'shared.domainName' },
      { key: 'dates.expiry', label: 'shared.expiryDate', kind: UiColumnKind.Date },
      { key: 'status', label: 'shared.status', kind: UiColumnKind.Status },
    ],
    pagination: true,
  },
  emptyText: 'reports.domainsReport.noDomainsFound',
}

export default schema
