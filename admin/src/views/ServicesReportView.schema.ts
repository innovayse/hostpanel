/**
 * Skeleton schema of the services report: the column picker and three date ranges, the print action, and the
 * paged table of the columns shown before the operator chooses any.
 */
import { REPORT_FIELDS_KEY } from '@/lib/reportsSchema'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { ReportAction } from '@/utils/ui/reportParts'
import { UiColumnKind } from '@/utils/ui/table'

/** What the screen is made of. */
const schema: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  actions: [ReportAction.Print],
  header: { title: 'reports.servicesReport.services', subtitle: 'reports.servicesReport.thisReportCanBeUsed' },
  filters: [
    { key: REPORT_FIELDS_KEY, label: 'shared.fieldsToInclude' },
    { key: 'created', label: 'reports.invoicesReport.creationDate' },
    { key: 'nextDue', label: 'shared.nextDueDate' },
    { key: 'terminated', label: 'reports.servicesReport.terminationDate' },
  ],
  table: {
    columns: [
      { key: 'id', label: 'shared.id', kind: UiColumnKind.Id },
      { key: 'clientName', label: 'shared.clientName' },
      { key: 'productName', label: 'orders.orderDetail.product' },
      { key: 'nextDueDate', label: 'shared.nextDueDate', kind: UiColumnKind.Date },
      { key: 'status', label: 'shared.status', kind: UiColumnKind.Status },
    ],
    pagination: true,
  },
  emptyText: 'reports.servicesReport.noServicesFound',
}

export default schema
