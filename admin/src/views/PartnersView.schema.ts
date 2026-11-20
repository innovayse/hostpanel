/**
 * What the storefront partners list is made of: a header with its add button and a draggable table of the partner logos,
 * a drag handle column first. `PartnersView` takes its captions from these columns and `UiSkeletonOf` draws the whole
 * list from the same data while the view's chunk is downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiColumnSize, UiCardRole } from '@/utils/ui/table'

/** The storefront partners list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.StorefrontPartners), subtitle: 'storefront.partners.subtitle', actions: 1 },
  table: {
    columns: [
      { key: 'handle', size: UiColumnSize.Compact, cardRole: UiCardRole.Hidden },
      { key: 'logo', label: 'storefront.partners.logo', size: UiColumnSize.Grow },
      { key: 'name', label: 'storefront.partners.name' },
      { key: 'website', label: 'storefront.partners.website' },
      { key: 'status', label: 'shared.status', cardRole: UiCardRole.Badge },
      { key: 'actions', label: 'shared.actions', actions: true },
    ],
  },
}

export default schema
