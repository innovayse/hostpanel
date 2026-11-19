/**
 * Skeleton schema of the income forecast report: the refresh action, the line chart of the running total over the table
 * of the months with their trailing windows, and the note naming the base currency.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import { ChartKind } from '@/utils/ui/chart'
import type { ChartReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { ReportAction, ReportNote } from '@/utils/ui/reportParts'
import { UiAlign, UiColumnSize } from '@/utils/ui/table'

/** What the income forecast report is made of. */
const schema: ChartReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  actions: [ReportAction.Refresh],
  header: { title: 'reports.incomeForecast.incomeForecast', subtitle: 'reports.incomeForecast.incomePerMonthWithTrailingWindows' },
  chart: { kind: ChartKind.Area },
  table: {
    columns: [
      { key: 'month', label: 'shared.month', size: UiColumnSize.Grow },
      { key: 'monthly', label: 'reports.incomeForecast.monthly', align: UiAlign.Right },
      { key: 'quarterly', label: 'reports.incomeForecast.quarterly', align: UiAlign.Right },
      { key: 'semiAnnual', label: 'reports.incomeForecast.semiAnnual', align: UiAlign.Right },
      { key: 'annual', label: 'reports.incomeForecast.annual', align: UiAlign.Right },
      { key: 'total', label: 'shared.total', align: UiAlign.Right },
    ],
  },
  note: ReportNote.Converted,
}

export default schema
