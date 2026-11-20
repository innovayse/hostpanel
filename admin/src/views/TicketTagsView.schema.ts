/**
 * Skeleton schema of the ticket tags report: a date range filter, a horizontal bar chart and the table of tags.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ChartReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { ChartKind, UiChartHeight } from '@/utils/ui/chart'
import { UiAlign, UiColumnSize } from '@/utils/ui/table'

/** What the screen is made of. */
const schema: ChartReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  header: { title: 'reports.ticketTags.ticketTagsOverview', subtitle: 'reports.ticketTags.thisReportProvidesAnOverview' },
  filters: [{ key: 'dateRange', label: 'shared.dateRange' }],
  chart: { kind: ChartKind.HorizontalBar, height: UiChartHeight.Md },
  table: {
    columns: [
      { key: 'tag', label: 'reports.ticketTags.tag', size: UiColumnSize.Grow },
      { key: 'count', label: 'reports.ticketTags.count', align: UiAlign.Right },
    ],
  },
  emptyText: 'shared.noDataFoundForThis2',
}

export default schema
