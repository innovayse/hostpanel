/**
 * What the manage users list is made of: a header, a search box and a paged table of the
 * identity users. `ManageUsersView` draws its table head from these columns and `UiSkeletonOf` draws the whole list from
 * the same data while the view's chunk is downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiColumnKind } from '@/utils/ui/table'

/** The manage users list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.ClientsUsers), subtitle: 'clients.manageUsers.viewAndManageUserAccounts' },
  toolbar: { search: true },
  table: {
    pagination: true,
    columns: [
      { key: 'id', label: 'shared.id', kind: UiColumnKind.Id },
      { key: 'firstName', label: 'shared.firstName' },
      { key: 'lastName', label: 'shared.lastName' },
      { key: 'email', label: 'shared.email' },
      { key: 'lastLogin', label: 'clients.manageUsers.lastLogin', kind: UiColumnKind.Date },
      { key: 'actions', actions: true },
    ],
  },
}

export default schema
