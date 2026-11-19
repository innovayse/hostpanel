/**
 * Skeleton schema of the customer retention report: the "include active products" checkbox over the table of
 * products grouped by product group. The group heading rows come with the data, so the table is `grouped` and the
 * skeleton opens its rows with a heading placeholder.
 */
import { SkeletonFieldKind, SkeletonKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiAlign, UiColumnSize } from '@/utils/ui/table'

/** What the customer retention report is made of. */
const schema: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  header: { title: 'reports.customerRetention.averageCustomerRetentionTime', subtitle: 'reports.customerRetention.thisReportCalculatesAndProvides' },
  filters: [
    { key: 'includeActive', label: 'reports.customerRetention.includeActiveProductsServicesAssuming', kind: SkeletonFieldKind.Check, span: SkeletonSpan.Full },
  ],
  table: {
    columns: [
      { key: 'productName', label: 'shared.productName', size: UiColumnSize.Grow },
      { key: 'billingCycle', label: 'shared.billingCycle' },
      { key: 'productCount', label: 'reports.customerRetention.productCount', align: UiAlign.Right },
      { key: 'avgDaysActive', label: 'reports.customerRetention.averageDaysActive', align: UiAlign.Right },
      { key: 'avgYearsMonthsActive', label: 'reports.customerRetention.averageYearsMonthsActive' },
    ],
    grouped: true,
  },
  emptyText: 'reports.customerRetention.noDataFound',
}

export default schema
