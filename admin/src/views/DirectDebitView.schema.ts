/**
 * Skeleton schema of the direct debit report: the refresh action, the header and the one table of unpaid invoices with
 * the client bank details. The report has no filters, tiles or chart, so its skeleton is a header over that table.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { ReportAction } from '@/utils/ui/reportParts'
import { UiAlign, UiColumnSize } from '@/utils/ui/table'

/** What the direct debit report is made of. */
const schema: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  actions: [ReportAction.Refresh],
  header: { title: 'reports.directDebit.directDebitProcessing', subtitle: 'reports.directDebit.thisReportDisplaysAllUnpaid' },
  table: {
    columns: [
      { key: 'invoiceId', label: 'shared.invoiceId', size: UiColumnSize.Compact },
      { key: 'clientName', label: 'shared.clientName', size: UiColumnSize.Grow },
      { key: 'invoiceDate', label: 'shared.invoiceDate', align: UiAlign.Center },
      { key: 'dueDate', label: 'shared.dueDate', align: UiAlign.Center },
      { key: 'subtotal', label: 'reports.directDebit.subtotal', align: UiAlign.Right },
      { key: 'tax', label: 'reports.directDebit.tax', align: UiAlign.Right },
      { key: 'credit', label: 'reports.directDebit.credit', align: UiAlign.Right },
      { key: 'total', label: 'shared.total', align: UiAlign.Right },
      { key: 'bankName', label: 'reports.directDebit.bankName' },
      { key: 'bankAccountType', label: 'reports.directDebit.bankAccountType' },
      { key: 'bankCode', label: 'reports.directDebit.bankCode' },
      { key: 'bankAccountNumber', label: 'reports.directDebit.bankAccountNumber' },
    ],
  },
  emptyText: 'shared.noDataFoundForThis',
}

export default schema
