/**
 * What the client domains tab is made of: a paged table of the client's domain registrations, with no toolbar.
 * `ClientDomainsListView` draws its table head from these columns and `UiSkeletonOf` draws the whole tab from the same
 * data while the view's chunk is still downloading. The tab sits under the client layout, whose header card is its own, so
 * the schema has no header of its own.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiColumnSize, UiCardRole } from '@/utils/ui/table'

/** The client domains tab. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  table: {
    pagination: true,
    columns: [
      { key: 'id', label: 'shared.id', size: UiColumnSize.Compact },
      { key: 'name', label: 'shared.domainName', size: UiColumnSize.Grow },
      { key: 'registrar', label: 'shared.registrar' },
      { key: 'status', label: 'shared.status', size: UiColumnSize.Compact, cardRole: UiCardRole.Badge },
      { key: 'expiresAt', label: 'shared.expiryDate', size: UiColumnSize.Compact },
      { key: 'autoRenew', label: 'shared.autoRenew', size: UiColumnSize.Compact },
    ],
  },
}

export default schema
