/**
 * What the tax rules list is made of: a header with the add button, a search box with the status filter, and a paged
 * table. `TaxRulesView` draws its table head from these columns and `UiSkeletonOf` draws the whole list from the same
 * data while the view's chunk is downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiAlign, UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** The tax rules list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.SettingsTaxRules), subtitle: 'taxRules.description', actions: 1 },
  toolbar: { search: true, filters: ['shared.status'] },
  table: {
    pagination: true,
    columns: [
      { key: 'name', label: 'taxRules.table.name', align: UiAlign.Left, size: UiColumnSize.Grow },
      { key: 'country', label: 'taxRules.table.country', align: UiAlign.Left },
      { key: 'region', label: 'taxRules.table.region', align: UiAlign.Left },
      { key: 'rate', label: 'taxRules.table.rate', numeric: true, size: UiColumnSize.Compact },
      { key: 'enabled', label: 'taxRules.table.enabled', align: UiAlign.Left, kind: UiColumnKind.Status },
      { key: 'actions', actions: true },
    ],
  },
}

export default schema
