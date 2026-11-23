/**
 * What the users tab of a client is made of: a heading with the add button over one table of the users linked to the
 * client. `ClientUsersView` draws its table head from these columns and `UiSkeletonOf` draws the tab from the same data
 * while the view's chunk is downloading. The heading carries the user count, which is data, so the header names no title.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** The users tab. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { actions: 1 },
  table: {
    columns: [
      { key: 'name', label: 'shared.name', size: UiColumnSize.Grow },
      { key: 'email', label: 'shared.email' },
      { key: 'role', label: 'clients.clientUsers.role' },
      { key: 'lastLogin', label: 'clients.clientUsers.lastLogin', kind: UiColumnKind.Date },
      { key: 'actions', label: 'shared.actions', actions: true },
    ],
  },
}

export default schema
