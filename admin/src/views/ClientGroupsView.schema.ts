/**
 * What the client groups screen is made of: a header with the add button and a table of the groups, with no toolbar and
 * no pager. `ClientGroupsView` draws its table head from these columns and `UiSkeletonOf` draws the whole screen from the
 * same data while the view's chunk is still downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** The client groups screen. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.ClientsGroups), subtitle: 'clients.clientGroups.subtitle', actions: 1 },
  table: {
    columns: [
      { key: 'name', label: 'shared.name' },
      { key: 'description', label: 'shared.description', size: UiColumnSize.Grow },
      { key: 'discount', label: 'clients.clientGroups.discount', size: UiColumnSize.Compact },
      { key: 'clients', label: 'clients.clientGroups.clients', size: UiColumnSize.Compact },
      { key: 'default', label: 'shared.default', kind: UiColumnKind.Status },
      { key: 'actions', actions: true },
    ],
  },
}

export default schema
