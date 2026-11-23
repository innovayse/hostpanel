/**
 * Skeleton schema of the monthly transactions report: the refresh action, the month and year filters, the line chart of
 * the days, the table of the days and the note naming the base currency. The title names the month once the report is
 * read; until then it is the name of the report.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ChartReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { ChartKind, UiChartHeight } from '@/utils/ui/chart'
import { ReportAction, ReportNote } from '@/utils/ui/reportParts'
import { UiAlign, UiColumnKind } from '@/utils/ui/table'

/** What the monthly transactions report is made of. */
const schema: ChartReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  actions: [ReportAction.Refresh],
  header: { title: 'reports.titles.reports.monthlyTransactions', subtitle: 'reports.monthlyTransactions.thisReportProvidesASummary' },
  filters: [
    { key: 'month', label: 'shared.month' },
    { key: 'year', label: 'shared.year' },
  ],
  chart: { kind: ChartKind.Line, height: UiChartHeight.Lg },
  table: {
    columns: [
      { key: 'date', label: 'shared.date', kind: UiColumnKind.Date },
      { key: 'amountIn', label: 'shared.amountIn', align: UiAlign.Right },
      { key: 'fees', label: 'shared.fees', align: UiAlign.Right },
      { key: 'amountOut', label: 'shared.amountOut', align: UiAlign.Right },
      { key: 'balance', label: 'shared.balance', align: UiAlign.Right },
    ],
  },
  note: ReportNote.Converted,
  emptyText: 'reports.monthlyTransactions.noTransactionsFoundForThis',
}

export default schema
