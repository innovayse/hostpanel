/**
 * What the plan changes list is made of: a header, one status select over the list, and a paged table with approve and
 * reject row actions. `ServiceChangeRequestsView` draws its table head from these columns and `UiSkeletonOf` draws the
 * whole list from the same data while the view's chunk is downloading. The header names the whole list; the view
 * switches to the history title when it is opened from one service.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiColumnKind, UiColumnSize, UiCardRole } from '@/utils/ui/table'

/** The plan changes list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: 'serviceChangeRequests.title', subtitle: 'serviceChangeRequests.subtitle' },
  toolbar: { filters: ['serviceChangeRequests.filterStatus'] },
  table: {
    pagination: true,
    columns: [
      { key: 'date', label: 'shared.date', kind: UiColumnKind.Date },
      { key: 'service', label: 'serviceChangeRequests.service', size: UiColumnSize.Compact },
      { key: 'client', label: 'shared.client' },
      { key: 'plans', label: 'serviceChangeRequests.plans', size: UiColumnSize.Grow },
      { key: 'type', label: 'shared.type', size: UiColumnSize.Compact },
      { key: 'proration', label: 'serviceChangeRequests.proration' },
      { key: 'status', label: 'shared.status', kind: UiColumnKind.Status, cardRole: UiCardRole.Badge },
      { key: 'actions', actions: true },
    ],
  },
}

export default schema
