/**
 * What the storefront locations list is made of: a header with its add button and a draggable table of the data-centre
 * locations, a drag handle column first. `LocationsView` takes its captions from these columns and `UiSkeletonOf` draws
 * the whole list from the same data while the view's chunk is downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiColumnSize, UiCardRole } from '@/utils/ui/table'

/** The storefront locations list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.StorefrontLocations), subtitle: 'storefront.locations.subtitle', actions: 1 },
  table: {
    columns: [
      { key: 'handle', size: UiColumnSize.Compact, cardRole: UiCardRole.Hidden },
      { key: 'country', label: 'storefront.locations.country', size: UiColumnSize.Grow },
      { key: 'name', label: 'storefront.locations.name' },
      { key: 'city', label: 'storefront.locations.city' },
      { key: 'status', label: 'shared.status', cardRole: UiCardRole.Badge },
      { key: 'actions', label: 'shared.actions', actions: true },
    ],
  },
}

export default schema
