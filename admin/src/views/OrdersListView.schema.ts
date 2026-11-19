/**
 * What the orders list is made of: a header with the review queue button, a search box with the status filter, and a
 * paged table with row actions. `OrdersListView` draws its table head from these columns and `UiSkeletonOf` draws the
 * whole list from the same data while the view's chunk is downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiCardRole, UiColumnSize } from '@/utils/ui/table'

/** The orders list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.Orders), subtitle: 'orders.ordersList.manageCustomerOrders', actions: 1 },
  toolbar: { search: true, filters: ['shared.status'] },
  table: {
    pagination: true,
    columns: [
      { key: 'order', label: 'orders.ordersList.order', cardRole: UiCardRole.Title, size: UiColumnSize.Compact },
      { key: 'client', label: 'shared.client', size: UiColumnSize.Grow, cardRole: UiCardRole.Subtitle },
      { key: 'status', label: 'shared.status', cardRole: UiCardRole.Badge },
      { key: 'ipAddress', label: 'shared.ipAddress' },
      { key: 'paymentMethod', label: 'shared.paymentMethod' },
      { key: 'total', label: 'shared.total', numeric: true, size: UiColumnSize.Compact },
      { key: 'items', label: 'orders.ordersList.items', numeric: true, size: UiColumnSize.Compact },
      { key: 'date', label: 'shared.date', size: UiColumnSize.Compact },
      { key: 'actions', label: 'shared.actions', actions: true },
    ],
  },
}

export default schema
