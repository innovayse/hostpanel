/**
 * What the product bundles list is made of: a header with its help link and the add button, and a table with no
 * toolbar and no pager. `ProductBundlesView` draws its table head from these columns and `UiSkeletonOf` draws the whole
 * list from the same data while the view's chunk is downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiColumnKind, UiColumnSize, UiCardRole } from '@/utils/ui/table'

/** The product bundles list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.SettingsProductBundles), subtitle: 'settings.productBundles.subtitle', actions: 2 },
  table: {
    columns: [
      { key: 'name', label: 'shared.name', size: UiColumnSize.Grow },
      { key: 'components', label: 'settings.productBundles.components' },
      { key: 'domain', label: 'settings.productBundles.domain' },
      { key: 'discount', label: 'settings.productBundles.discount' },
      { key: 'status', label: 'shared.status', kind: UiColumnKind.Status, cardRole: UiCardRole.Badge },
      { key: 'actions', label: 'shared.actions', actions: true },
    ],
  },
}

export default schema
