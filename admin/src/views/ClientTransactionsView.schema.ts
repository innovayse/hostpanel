/**
 * What the client transactions tab is made of: a header with the add button, the four balance tiles, a search box and the
 * four filters of its dialog over a paged table of the client's transactions. `ClientTransactionsView` draws its table head
 * from these columns and `UiSkeletonOf` draws the whole tab from the same data while the view's chunk is still downloading.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema, SkeletonStat } from '@/utils/ui/skeletonSchema'
import { UiColumnSize } from '@/utils/ui/table'

/** The four balance tiles. */
export const CLIENT_TRANSACTIONS_STATS: readonly SkeletonStat[] = [
  { key: 'totalIn', label: 'clients.clientTransactions.totalIn' },
  { key: 'totalFees', label: 'clients.clientTransactions.totalFees' },
  { key: 'totalOut', label: 'clients.clientTransactions.totalOut' },
  { key: 'balance', label: 'shared.balance' },
]

/** The client transactions tab. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: 'shared.transactions', actions: 1 },
  stats: CLIENT_TRANSACTIONS_STATS,
  toolbar: {
    search: true,
    filters: ['billing.transactionsList.show', 'shared.paymentMethod', 'shared.dateRange', 'shared.amount'],
  },
  table: {
    pagination: true,
    columns: [
      { key: 'date', label: 'shared.date' },
      { key: 'paymentMethod', label: 'shared.paymentMethod' },
      { key: 'description', label: 'shared.description', size: UiColumnSize.Grow },
      { key: 'amountIn', label: 'shared.amountIn', numeric: true },
      { key: 'amountOut', label: 'shared.amountOut', numeric: true },
      { key: 'fees', label: 'shared.fees', numeric: true },
      { key: 'actions', label: 'shared.actions', actions: true },
    ],
  },
}

export default schema
