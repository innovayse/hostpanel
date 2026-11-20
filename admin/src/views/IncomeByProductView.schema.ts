/**
 * Skeleton schema of the income by product report: the refresh action, the month and year filters over the table of products
 * grouped by product group (the table it shares with the monthly orders report), and the note naming the base currency. The title names the month once the report is
 * read; until then it is the report's own name.
 */
import { PRODUCT_SALES_TABLE } from '@/lib/reportsSalesTable'
import { ReportAction, ReportNote } from '@/utils/ui/reportParts'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** What the income by product report is made of. */
const schema: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  actions: [ReportAction.Refresh],
  header: { title: 'reports.titles.reports.incomeByProduct', subtitle: 'reports.incomeByProduct.thisReportGivesABreakdown' },
  filters: [
    { key: 'month', label: 'shared.month' },
    { key: 'year', label: 'shared.year' },
  ],
  table: PRODUCT_SALES_TABLE,
  note: ReportNote.Converted,
}

export default schema
