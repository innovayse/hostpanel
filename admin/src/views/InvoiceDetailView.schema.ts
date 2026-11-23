/**
 * Skeleton schema of the invoice screen: the header (its title is made of the invoice number) and the cards of the
 * summary tab, the tab the screen opens on: the four facts, who is billed, the line items and the two transaction
 * ledgers.
 *
 * The other tabs wait for the invoice and draw no placeholder of their own, so the summary is what a skeleton draws.
 * The real screen draws the same cards through its summary tab once the invoice has been read.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { DetailSkeletonSchema, SkeletonTable } from '@/utils/ui/skeletonSchema'
import { UiAlign, UiCardRole, UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** The read-only line items: the description and the amount; the real card heads its table from these columns. */
export const INVOICE_ITEMS_TABLE: SkeletonTable = {
  columns: [
    { key: 'description', label: 'shared.description', size: UiColumnSize.Grow },
    { key: 'amount', label: 'shared.amount', align: UiAlign.Right, numeric: true, size: UiColumnSize.Compact },
  ],
  rows: 3,
}

/** What the invoice screen is made of. */
const schema: DetailSkeletonSchema = {
  kind: SkeletonKind.Detail,
  header: { subtitle: 'billing.invoiceDetail.manageAndViewInvoiceDetails', actions: 3 },
  columns: [
    [
      {
        key: 'facts',
        rows: ['shared.invoiceDate', 'shared.dueDate', 'billing.invoiceSummaryTab.invoiceAmount', 'shared.balance'],
      },
      { key: 'document', rows: ['shared.clientName', 'billing.invoiceSummaryTab.paymentMethod'] },
      {
        key: 'items',
        title: 'shared.invoiceItems',
        table: INVOICE_ITEMS_TABLE,
      },
      {
        key: 'payments',
        title: 'shared.transactions',
        table: {
          columns: [
            { key: 'date', label: 'shared.date', kind: UiColumnKind.Date },
            { key: 'method', label: 'shared.paymentMethod', size: UiColumnSize.Grow },
            { key: 'transactionId', label: 'shared.transactionId' },
            { key: 'amount', label: 'shared.amount', align: UiAlign.Right, numeric: true, size: UiColumnSize.Compact },
            { key: 'fees', label: 'shared.transactionFees', align: UiAlign.Right, numeric: true, size: UiColumnSize.Compact },
          ],
          rows: 2,
        },
      },
      {
        key: 'history',
        title: 'shared.transactionHistory',
        table: {
          columns: [
            { key: 'date', label: 'shared.date', kind: UiColumnKind.Date },
            { key: 'method', label: 'shared.paymentMethod' },
            { key: 'transactionId', label: 'shared.transactionId' },
            { key: 'status', label: 'shared.status', kind: UiColumnKind.Status, cardRole: UiCardRole.Badge },
            { key: 'description', label: 'shared.description', size: UiColumnSize.Grow },
          ],
          rows: 2,
        },
      },
    ],
  ],
}

export default schema
