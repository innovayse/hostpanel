/**
 * Skeleton schema of the aging invoices report: the refresh action, the pie of the overdue periods over the table of periods.
 *
 * The table has one amount column per currency of the report, which the data alone decides, so the schema declares
 * the period column and a single `amount` placeholder column that the screen replaces with the currency columns once
 * the report is read. The pie and the table are drawn while the report loads, so skeleton and screen have the same
 * blocks.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ChartReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { ChartKind, UiChartHeight } from '@/utils/ui/chart'
import { ReportAction } from '@/utils/ui/reportParts'
import { UiAlign, UiColumnSize } from '@/utils/ui/table'

/** What the aging invoices report is made of. */
const schema: ChartReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  actions: [ReportAction.Refresh],
  header: { title: 'reports.agingInvoices.agingInvoices', subtitle: 'reports.agingInvoices.aSummaryOfOutstandingInvoices' },
  chart: { kind: ChartKind.Donut, height: UiChartHeight.Lg },
  table: {
    columns: [
      { key: 'period', label: 'reports.agingInvoices.period', size: UiColumnSize.Grow },
      { key: 'amount', label: 'shared.amount', align: UiAlign.Right },
    ],
  },
}

export default schema
