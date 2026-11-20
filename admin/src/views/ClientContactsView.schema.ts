/**
 * What the contacts tab of a client is made of: one table of the client's contacts. `ClientContactsView` draws its table
 * head from these columns and `UiSkeletonOf` draws the table from the same data while the view's chunk is downloading.
 * The section title over the table carries the contact count, which is data, so the header names no title, only its add button.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiColumnSize } from '@/utils/ui/table'

/** The contacts tab. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { actions: 1 },
  table: {
    columns: [
      { key: 'name', label: 'shared.name', size: UiColumnSize.Grow },
      { key: 'email', label: 'shared.email' },
      { key: 'phone', label: 'shared.phone' },
      { key: 'type', label: 'shared.type', size: UiColumnSize.Compact },
      { key: 'actions', label: 'shared.actions', actions: true },
    ],
  },
}

export default schema
