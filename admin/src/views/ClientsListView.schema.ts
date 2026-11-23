/**
 * What the clients list is made of: a header with the add button, a search box and the four filters of its dialog, and
 * a paged table. `ClientsListView` draws its table head from these columns and `UiSkeletonOf` draws the whole list from
 * the same data while the view's chunk is still downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiCardRole, UiColumnKind, UiColumnSize, UiHideBelow } from '@/utils/ui/table'

/** The clients list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.Clients), subtitle: 'clients.clientsList.manageYourClientAccounts', actions: 1 },
  toolbar: {
    search: true,
    filters: ['shared.emailAddress', 'shared.phoneNumber', 'shared.status', 'clients.clientGroups.group'],
  },
  table: {
    pagination: true,
    columns: [
      { key: 'id', label: 'shared.id', hideBelow: UiHideBelow.Md, kind: UiColumnKind.Id },
      { key: 'name', label: 'shared.name', size: UiColumnSize.Grow },
      { key: 'email', label: 'shared.email', hideBelow: UiHideBelow.Lg, cardRole: UiCardRole.Subtitle },
      { key: 'company', label: 'shared.company', hideBelow: UiHideBelow.Xl },
      { key: 'status', label: 'shared.status', cardRole: UiCardRole.Badge, kind: UiColumnKind.Status },
      { key: 'group', label: 'clients.clientGroups.group', hideBelow: UiHideBelow.Xl },
      { key: 'twoFactor', label: 'clients.clientsList.2fa', hideBelow: UiHideBelow.Xl, kind: UiColumnKind.Status },
      { key: 'joined', label: 'clients.clientsList.joined', hideBelow: UiHideBelow.Lg, kind: UiColumnKind.Date },
      { key: 'actions', actions: true },
    ],
  },
}

export default schema
