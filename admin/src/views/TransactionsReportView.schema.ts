/**
 * Skeleton schema of the transactions report: the column picker and the date range, the print and export
 * actions, and the paged table of the columns shown before the operator chooses any.
 */
import { REPORT_FIELDS_KEY } from '@/lib/reportsSchema'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { ReportAction } from '@/utils/ui/reportParts'

/** What the screen is made of. */
const schema: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  actions: [ReportAction.Export, ReportAction.Print],
  header: { title: 'shared.transactions', subtitle: 'reports.transactionsReport.thisReportCanBeUsed' },
  filters: [{ key: REPORT_FIELDS_KEY, label: 'shared.fieldsToInclude' }, { key: 'date', label: 'shared.dateRange' }],
  table: {
    columns: [
      { key: 'id', label: 'shared.id' },
      { key: 'clientName', label: 'shared.clientName' },
      { key: 'date', label: 'shared.date' },
      { key: 'paymentMethod', label: 'shared.paymentMethod' },
      { key: 'amountIn', label: 'shared.amountIn' },
    ],
    pagination: true,
  },
  emptyText: 'reports.transactionsReport.noTransactionsFound',
}

export default schema
