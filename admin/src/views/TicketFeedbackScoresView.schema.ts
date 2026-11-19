/**
 * Skeleton schema of the ticket feedback scores report: a date range filter, a bar chart of the averages and the
 * table of staff members with their rating distribution, the number of ratings and the average.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ChartReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { ChartKind, UiChartHeight } from '@/utils/ui/chart'
import { UiAlign, UiColumnSize } from '@/utils/ui/table'

/** What the screen is made of. */
const schema: ChartReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  header: { title: 'reports.ticketFeedbackScores.ticketFeedbackScores', subtitle: 'reports.ticketFeedbackScores.thisReportProvidesASummary' },
  filters: [{ key: 'dateRange', label: 'shared.dateRange' }],
  chart: { kind: ChartKind.GroupedBar, height: UiChartHeight.Lg },
  table: {
    columns: [
      { key: 'staffName', label: 'reports.ticketFeedbackScores.staffName', size: UiColumnSize.Grow },
      { key: 'distribution', label: 'reports.ticketFeedbackScores.distribution' },
      { key: 'totalRatings', label: 'reports.ticketFeedbackScores.totalRatings', align: UiAlign.Right },
      { key: 'averageRating', label: 'reports.ticketFeedbackScores.averageRating', align: UiAlign.Right },
    ],
  },
  emptyText: 'shared.noDataFoundForThis2',
}

export default schema
