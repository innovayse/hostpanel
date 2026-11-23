/**
 * What the billable items list is made of: a header with the add button, a search box with the client, type and invoiced
 * filters, and a paged, selectable table. `BillableItemsView` draws its table head from these columns and `UiSkeletonOf`
 * draws the whole list from the same data while the view's chunk is downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** The billable items list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.BillingBillableItems), subtitle: 'billing.billableItems.manageUninvoicedAndRecurringCharges', actions: 1 },
  toolbar: { search: true, filters: ['shared.client', 'shared.type', 'billing.billableItems.invoiced'] },
  table: {
    pagination: true,
    columns: [
      { key: 'id', label: 'shared.id', kind: UiColumnKind.Id },
      { key: 'client', label: 'shared.clientName' },
      { key: 'description', label: 'shared.description', size: UiColumnSize.Grow },
      { key: 'amount', label: 'shared.amount', numeric: true },
      { key: 'type', label: 'shared.type' },
      { key: 'invoiced', label: 'billing.billableItems.invoiced', kind: UiColumnKind.Status },
    ],
  },
}

export default schema
