/**
 * What the cancellation requests list is made of: a header, a search box with the type and status filters, and a paged
 * table with a delete action per row. `CancellationRequestsView` draws its table head from these columns and
 * `UiSkeletonOf` draws the whole list from the same data while the view's chunk is downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiColumnKind, UiColumnSize, UiCardRole } from '@/utils/ui/table'

/** The cancellation requests list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.ClientsCancellations), subtitle: 'clients.cancellationRequests.reviewAndProcessServiceCancellation' },
  toolbar: { search: true, filters: ['shared.type', 'shared.status'] },
  table: {
    pagination: true,
    columns: [
      { key: 'date', label: 'shared.date', kind: UiColumnKind.Date },
      { key: 'service', label: 'shared.productService', size: UiColumnSize.Grow },
      { key: 'client', label: 'shared.client' },
      { key: 'reason', label: 'shared.reason' },
      { key: 'type', label: 'shared.type', kind: UiColumnKind.Status },
      { key: 'status', label: 'shared.status', kind: UiColumnKind.Status, cardRole: UiCardRole.Badge },
      { key: 'actions', actions: true },
    ],
  },
}

export default schema
