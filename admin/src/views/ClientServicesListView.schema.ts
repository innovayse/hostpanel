/**
 * What the client services tab is made of: a search box and the three filters of its dialog (status, product and billing
 * cycle) over a paged table of the client's services. `ClientServicesListView` draws its table head from these columns and
 * `UiSkeletonOf` draws the whole tab from the same data while the view's chunk is still downloading. The tab sits under the
 * client layout, whose header card is its own, so the schema has no header of its own.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiCardRole, UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** The client services tab. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  toolbar: {
    search: true,
    filters: ['shared.status', 'shared.productService', 'shared.billingCycle'],
  },
  table: {
    pagination: true,
    columns: [
      { key: 'id', label: 'shared.id', kind: UiColumnKind.Id },
      { key: 'product', label: 'shared.productService', size: UiColumnSize.Grow },
      { key: 'domain', label: 'shared.domain' },
      { key: 'price', label: 'shared.price', numeric: true },
      { key: 'billingCycle', label: 'shared.billingCycle' },
      { key: 'nextDue', label: 'clients.clientServicesList.nextDue', kind: UiColumnKind.Date },
      { key: 'status', label: 'shared.status', kind: UiColumnKind.Status, cardRole: UiCardRole.Badge },
    ],
  },
}

export default schema
