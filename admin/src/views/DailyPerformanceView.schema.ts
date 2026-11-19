/**
 * Skeleton schema of the daily performance report: the refresh action, the date range filter, four tiles (orders, paid
 * invoices, tickets, cancellations), the line chart and the table of the days, with the empty sentence. The table has
 * the day and one column per counter; the four tiles are the counters whose keys they share.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ChartReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { ChartKind, UiChartHeight } from '@/utils/ui/chart'
import { ReportAction } from '@/utils/ui/reportParts'
import { UiAlign, UiColumnSize } from '@/utils/ui/table'

/** What the daily performance report is made of. */
const schema: ChartReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  actions: [ReportAction.Refresh],
  header: { title: 'reports.dailyPerformance.dailyPerformance', subtitle: 'reports.dailyPerformance.overviewOfDailyActivityMetrics' },
  filters: [{ key: 'dateRange', label: 'shared.dateRange' }],
  stats: [
    { key: 'completedOrders', label: 'reports.dailyPerformance.completedOrders' },
    { key: 'paidInvoices', label: 'reports.dailyPerformance.paidInvoices' },
    { key: 'openedTickets', label: 'reports.dailyPerformance.openedTickets' },
    { key: 'cancellationRequests', label: 'reports.dailyPerformance.cancellationRequests' },
  ],
  chart: { kind: ChartKind.Line, height: UiChartHeight.Lg },
  table: {
    columns: [
      { key: 'date', label: 'shared.date', size: UiColumnSize.Grow },
      { key: 'completedOrders', label: 'reports.dailyPerformance.completedOrders', align: UiAlign.Right },
      { key: 'newInvoices', label: 'reports.dailyPerformance.newInvoices', align: UiAlign.Right },
      { key: 'paidInvoices', label: 'reports.dailyPerformance.paidInvoices', align: UiAlign.Right },
      { key: 'openedTickets', label: 'reports.dailyPerformance.openedTickets', align: UiAlign.Right },
      { key: 'ticketReplies', label: 'reports.dailyPerformance.ticketReplies', align: UiAlign.Right },
      { key: 'cancellationRequests', label: 'reports.dailyPerformance.cancellationRequests', align: UiAlign.Right },
    ],
  },
  emptyText: 'reports.dailyPerformance.noDataFound',
}

export default schema
