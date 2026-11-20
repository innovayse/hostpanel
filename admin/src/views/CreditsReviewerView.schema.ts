/**
 * Skeleton schema of the credits reviewer: the refresh action, the client, date range and amount range filters, the
 * summary lines (one per currency, which the data decides) over the paged table of credits, and the sentence shown when
 * the filters match no credit.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { ReportAction } from '@/utils/ui/reportParts'
import { UiAlign, UiColumnSize } from '@/utils/ui/table'

/** What the credits reviewer is made of. */
const schema: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  actions: [ReportAction.Refresh],
  header: { title: 'reports.creditsReviewer.creditsReviewer', subtitle: 'reports.creditsReviewer.thisReportAllowsYouTo' },
  filters: [
    { key: 'client', label: 'shared.client' },
    { key: 'dateRange', label: 'shared.dateRange' },
    { key: 'minAmount', label: 'reports.creditsReviewer.minAmount' },
    { key: 'maxAmount', label: 'reports.creditsReviewer.maxAmount' },
  ],
  summary: true,
  table: {
    columns: [
      { key: 'id', label: 'reports.creditsReviewer.creditId', size: UiColumnSize.Compact },
      { key: 'clientId', label: 'reports.creditsReviewer.clientId', size: UiColumnSize.Compact },
      { key: 'clientName', label: 'shared.clientName' },
      { key: 'date', label: 'shared.date', align: UiAlign.Center, size: UiColumnSize.Compact },
      { key: 'description', label: 'shared.description', size: UiColumnSize.Grow },
      { key: 'currency', label: 'shared.currency', size: UiColumnSize.Compact },
      { key: 'amount', label: 'shared.amount', align: UiAlign.Right },
    ],
    pagination: true,
  },
  emptyText: 'reports.creditsReviewer.noCreditsFoundForThis',
}

export default schema
