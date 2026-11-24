/**
 * Skeleton schema of the sales tax report: the refresh action, a date range filter, the summary lines (one per
 * currency, which the data decides) over the paged table of invoices with their tax, and the sentence shown when the
 * period has no invoice.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { ReportAction } from '@/utils/ui/reportParts'
import { UiColumnKind, UiAlign, UiColumnSize } from '@/utils/ui/table'

/** What the screen is made of. */
const schema: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  actions: [ReportAction.Refresh],
  header: { title: 'reports.salesTax.salesTaxLiability', subtitle: 'reports.salesTax.thisReportShowsSalesTax' },
  filters: [{ key: 'dateRange', label: 'shared.dateRange' }],
  summary: true,
  table: {
    columns: [
      { key: 'id', label: 'shared.invoiceId', kind: UiColumnKind.Id },
      { key: 'clientName', label: 'shared.clientName', size: UiColumnSize.Grow },
      { key: 'invoiceDate', label: 'shared.invoiceDate', kind: UiColumnKind.Date },
      { key: 'datePaid', label: 'shared.datePaid', kind: UiColumnKind.Date },
      { key: 'currency', label: 'shared.currency', size: UiColumnSize.Compact },
      { key: 'subTotal', label: 'shared.subTotal2', align: UiAlign.Right },
      { key: 'tax', label: 'reports.salesTax.tax', align: UiAlign.Right },
      { key: 'credit', label: 'reports.salesTax.credit', align: UiAlign.Right },
      { key: 'total', label: 'shared.total', align: UiAlign.Right },
    ],
    pagination: true,
  },
  emptyText: 'reports.salesTax.noInvoicesFoundForThis',
}

export default schema
