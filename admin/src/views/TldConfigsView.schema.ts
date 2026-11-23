/**
 * What the TLD pricing list is made of: a header with the import, price sync and add controls, a search box with the
 * registrar and status filters, and a paged table. `TldConfigsView` draws its table head from these columns and
 * `UiSkeletonOf` draws the whole list from the same data while the view's chunk is still downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiAlign, UiCardRole, UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** The TLD pricing list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: {
    title: navTitleKey(AppRoute.SettingsTldPricing),
    subtitle: 'settings.tldConfigs.configureDomainExtensionPricingAnd',
    actions: 3,
    primaryAction: 1,
  },
  toolbar: { search: true, filters: ['shared.registrar', 'shared.status'] },
  table: {
    pagination: true,
    columns: [
      { key: 'tld', label: 'settings.tldConfigs.tld', align: UiAlign.Left, size: UiColumnSize.Grow },
      { key: 'provider', label: 'settings.tldConfigs.provider', align: UiAlign.Left },
      { key: 'status', label: 'shared.status', align: UiAlign.Left, kind: UiColumnKind.Status, cardRole: UiCardRole.Badge },
      { key: 'cost', label: 'settings.tldConfigs.cost1yr', numeric: true, size: UiColumnSize.Compact },
      { key: 'sell', label: 'settings.tldConfigs.sell1yr', numeric: true, size: UiColumnSize.Compact },
      { key: 'margin', label: 'settings.tldConfigs.margin', numeric: true, size: UiColumnSize.Compact },
      { key: 'categories', label: 'shared.categories', align: UiAlign.Left },
      { key: 'lastSynced', label: 'settings.tldConfigs.lastSynced', align: UiAlign.Left, kind: UiColumnKind.Date },
      { key: 'actions', label: 'shared.actions', actions: true },
    ],
  },
}

export default schema
