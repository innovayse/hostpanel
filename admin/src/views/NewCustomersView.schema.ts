/**
 * Skeleton schema of the new customers report: a year filter, a line chart and a table of the months with the
 * conversion rate of each.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ChartReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { ChartKind, UiChartHeight } from '@/utils/ui/chart'
import { UiAlign, UiColumnSize } from '@/utils/ui/table'

/** What the screen is made of. */
const schema: ChartReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  header: { title: 'reports.newCustomers.newCustomers', subtitle: 'reports.newCustomers.monthlyNewSignupsAndOrder' },
  filters: [{ key: 'year', label: 'shared.year' }],
  chart: { kind: ChartKind.Line, height: UiChartHeight.Md },
  table: {
    columns: [
      { key: 'month', label: 'shared.month', size: UiColumnSize.Grow },
      { key: 'newSignups', label: 'reports.newCustomers.newSignups', align: UiAlign.Right },
      { key: 'ordersPlaced', label: 'reports.newCustomers.ordersPlaced', align: UiAlign.Right },
      { key: 'ordersCompleted', label: 'reports.newCustomers.ordersCompleted', align: UiAlign.Right },
      { key: 'conversionRate', label: 'reports.newCustomers.conversionRate', align: UiAlign.Right },
    ],
  },
}

export default schema
