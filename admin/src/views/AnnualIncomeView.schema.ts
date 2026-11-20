/**
 * Skeleton schema of the annual income report: the refresh action, the year filter, three tiles (total, monthly
 * average, best month), the bar chart of the months, the table of the months and the note naming the base currency.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import { ChartKind } from '@/utils/ui/chart'
import type { ChartReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { ReportAction, ReportNote } from '@/utils/ui/reportParts'
import { UiAlign, UiColumnSize } from '@/utils/ui/table'

/** What the annual income report is made of. */
const schema: ChartReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  actions: [ReportAction.Refresh],
  header: { title: 'reports.annualIncome.annualIncomeReport', subtitle: 'reports.annualIncome.totalIncomePerMonthFor' },
  filters: [{ key: 'year', label: 'shared.year' }],
  stats: [
    { key: 'total', label: 'reports.annualIncome.totalIncome' },
    { key: 'average', label: 'reports.annualIncome.averageMonth' },
    { key: 'best', label: 'reports.annualIncome.bestMonth' },
  ],
  chart: { kind: ChartKind.GroupedBar },
  table: {
    columns: [
      { key: 'month', label: 'shared.month', size: UiColumnSize.Grow },
      { key: 'amount', label: 'shared.amount', align: UiAlign.Right },
    ],
  },
  note: ReportNote.Converted,
}

export default schema
