/**
 * Skeleton schema of the monthly orders report: the month and year filters over the table of products grouped by
 * product group (the table it shares with the income by product report), and the note naming the base currency. The title names the month once the report is
 * read; until then it is the report's own name.
 */
import { PRODUCT_SALES_TABLE } from '@/lib/reportsSalesTable'
import { ReportNote } from '@/utils/ui/reportParts'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** What the monthly orders report is made of. */
const schema: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  header: { title: 'reports.titles.reports.monthlyOrders', subtitle: 'reports.monthlyOrders.thisReportGivesABreakdown' },
  filters: [
    { key: 'month', label: 'shared.month' },
    { key: 'year', label: 'shared.year' },
  ],
  table: PRODUCT_SALES_TABLE,
  note: ReportNote.Converted,
}

export default schema
