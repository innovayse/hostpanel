/**
 * Skeleton schema of the product suspensions report: the refresh action and the table of suspended services with their reason.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { ReportAction } from '@/utils/ui/reportParts'
import { UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** What the screen is made of. */
const schema: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  actions: [ReportAction.Refresh],
  header: { title: 'reports.productSuspensions.productSuspensions', subtitle: 'reports.productSuspensions.thisReportAllowsYouTo' },
  table: {
    columns: [
      { key: 'serviceId', label: 'reports.productSuspensions.serviceId', kind: UiColumnKind.Id },
      { key: 'clientName', label: 'shared.clientName', size: UiColumnSize.Grow },
      { key: 'productName', label: 'shared.productName' },
      { key: 'domain', label: 'shared.domain' },
      { key: 'nextDueDate', label: 'shared.nextDueDate', kind: UiColumnKind.Date },
      { key: 'suspendReason', label: 'reports.productSuspensions.suspendReason' },
    ],
  },
  emptyText: 'reports.productSuspensions.noSuspendedServices',
}

export default schema
