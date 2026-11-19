/**
 * What the network issues list is made of: a header with the create button, a search box with the status, type and
 * priority filters, and a paged table. `NetworkIssuesView` draws its table head from these columns and `UiSkeletonOf`
 * draws the whole list from the same data while the view's chunk is still downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiColumnSize, UiCardRole } from '@/utils/ui/table'

/** The network issues list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.SupportNetworkIssues), subtitle: 'support.networkIssues.manageNetworkStatusAndIncident', actions: 1 },
  toolbar: { search: true, filters: ['shared.status', 'shared.type', 'shared.priority'] },
  table: {
    pagination: true,
    columns: [
      { key: 'title', label: 'shared.title', size: UiColumnSize.Grow },
      { key: 'type', label: 'shared.type', size: UiColumnSize.Compact },
      { key: 'priority', label: 'shared.priority', size: UiColumnSize.Compact },
      { key: 'status', label: 'shared.status', size: UiColumnSize.Compact, cardRole: UiCardRole.Badge },
      { key: 'startDate', label: 'support.networkIssues.startDate' },
      { key: 'endDate', label: 'support.networkIssues.endDate' },
      { key: 'actions', label: 'shared.actions', actions: true },
    ],
  },
}

export default schema
