/**
 * Skeleton schema of the top clients report: the refresh and export actions, three stat tiles, a pie chart, the table of
 * the ranked clients and the note naming the base currency.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ChartReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { ChartKind, UiChartHeight } from '@/utils/ui/chart'
import { ReportAction, ReportNote } from '@/utils/ui/reportParts'
import { UiAlign, UiColumnSize } from '@/utils/ui/table'

/** What the screen is made of. */
const schema: ChartReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  header: { title: 'reports.topClients.top10ClientsByIncome', subtitle: 'reports.topClients.thisReportShowsThe10' },
  actions: [ReportAction.Refresh, ReportAction.Export],
  stats: [
    { key: 'leader', label: 'reports.topClients.leader' },
    { key: 'total', label: 'reports.topClients.totalIncome' },
    { key: 'clients', label: 'reports.topClients.rankedClients' },
  ],
  chart: { kind: ChartKind.Donut, height: UiChartHeight.Lg },
  table: {
    columns: [
      { key: 'clientId', label: 'reports.creditsReviewer.clientId', size: UiColumnSize.Compact },
      { key: 'clientName', label: 'shared.clientName', size: UiColumnSize.Grow },
      { key: 'totalAmountIn', label: 'shared.amountIn', align: UiAlign.Right },
      { key: 'totalFees', label: 'shared.fees', align: UiAlign.Right },
      { key: 'totalAmountOut', label: 'shared.amountOut', align: UiAlign.Right },
      { key: 'balance', label: 'shared.balance', align: UiAlign.Right },
    ],
  },
  note: ReportNote.Converted,
  emptyText: 'reports.topClients.noData',
}

export default schema
