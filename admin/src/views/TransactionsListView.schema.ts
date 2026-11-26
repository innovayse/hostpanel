/**
 * What the transactions list is made of: a header with its add button, four summary tiles, a search box with the client,
 * direction, payment method, date range and amount filters, and a paged table. `TransactionsListView` draws its table
 * head from these columns and `UiSkeletonOf` draws the whole list from the same data while the view's chunk is downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiCardRole, UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** The transactions list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.BillingTransactions), subtitle: 'billing.transactionsList.paymentTransactionsAndRecords', actions: 1 },
  stats: [
    { key: 'totalIn', label: 'billing.transactionsList.totalIncome' },
    { key: 'totalOut', label: 'billing.transactionsList.totalExpenditure' },
    { key: 'totalFees', label: 'billing.transactionsList.totalFees' },
    { key: 'balance', label: 'shared.balance' },
  ],
  toolbar: {
    search: true,
    filters: ['shared.client', 'billing.transactionsList.show', 'shared.paymentMethod', 'shared.dateRange', 'shared.amount'],
  },
  table: {
    pagination: true,
    columns: [
      { key: 'client', label: 'shared.clientName', cardRole: UiCardRole.Title },
      { key: 'date', label: 'shared.date', kind: UiColumnKind.Date },
      { key: 'paymentMethod', label: 'shared.paymentMethod' },
      { key: 'description', label: 'shared.description', size: UiColumnSize.Grow },
      { key: 'amountIn', label: 'shared.amountIn', numeric: true, size: UiColumnSize.Compact },
      { key: 'fees', label: 'shared.fees', numeric: true, size: UiColumnSize.Compact },
      { key: 'amountOut', label: 'shared.amountOut', numeric: true, size: UiColumnSize.Compact },
      { key: 'actions', actions: true },
    ],
  },
}

export default schema
