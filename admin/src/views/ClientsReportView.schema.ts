/**
 * Skeleton schema of the clients report: the print action, the field picker and the creation date range, and the
 * table with the columns shown before the operator chooses others (those keys are also the screen's default columns),
 * with its pager.
 */
import { REPORT_FIELDS_KEY } from '@/lib/reportsSchema'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { ReportAction } from '@/utils/ui/reportParts'

/** What the clients report is made of. */
const schema: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  actions: [ReportAction.Print],
  header: { title: 'shared.clients', subtitle: 'reports.clientsReport.thisReportCanBeUsed' },
  filters: [
    { key: REPORT_FIELDS_KEY, label: 'shared.fieldsToInclude' },
    { key: 'created', label: 'reports.clientsReport.creationDateRange' },
  ],
  table: {
    columns: [
      { key: 'id', label: 'shared.id' },
      { key: 'firstName', label: 'shared.firstName' },
      { key: 'lastName', label: 'shared.lastName' },
      { key: 'email', label: 'shared.email' },
      { key: 'status', label: 'shared.status' },
    ],
    pagination: true,
  },
  emptyText: 'reports.clientsReport.noClientsFoundClickFilter',
}

export default schema
