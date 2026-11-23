/**
 * What the products list is made of: a header with its help link and the create button, a search box with the group,
 * type and status filters, and a paged table with one price column per billing cycle (the cycles the product form lists,
 * `PRODUCT_PRICE_CYCLES`, so the columns and the form have one source). `ProductsView` draws its table
 * head from these columns and `UiSkeletonOf` draws the whole list from the same data while the view's chunk is
 * downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { PRODUCT_PRICE_CYCLES } from '@/types/product'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema, SkeletonColumn } from '@/utils/ui/skeletonSchema'
import { UiColumnKind, UiColumnSize, UiCardRole } from '@/utils/ui/table'

/** One compact money column per billing cycle, captioned by the product form's own cycle label. */
const priceColumns: readonly SkeletonColumn[] = PRODUCT_PRICE_CYCLES.map((cycle) => ({
  key: cycle,
  label: `settings.productForm.${cycle}`,
  numeric: true,
  size: UiColumnSize.Compact,
}))

/** The products list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.SettingsProducts), subtitle: 'settings.products.manageHostingPlansAndServices', actions: 2 },
  toolbar: { search: true, filters: ['settings.products.group', 'shared.type', 'shared.status'] },
  table: {
    pagination: true,
    columns: [
      { key: 'name', label: 'shared.name', size: UiColumnSize.Grow },
      { key: 'group', label: 'settings.products.group' },
      { key: 'type', label: 'shared.type' },
      ...priceColumns,
      { key: 'status', label: 'shared.status', kind: UiColumnKind.Status, cardRole: UiCardRole.Badge },
      { key: 'actions', label: 'shared.actions', actions: true },
    ],
  },
}

export default schema
