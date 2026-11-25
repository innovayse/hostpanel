/**
 * What the VAT rates list is made of: a header with the add button, a search box with the kind and status filters, and a paged table.
 * `VatRatesView` draws its table head from these columns and `UiSkeletonOf` draws the whole list from the same data
 * while the view's chunk is downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiAlign, UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** The VAT rates list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.SettingsVatRates), subtitle: 'vatRates.description', actions: 1 },
  toolbar: { search: true, filters: ['vatRates.table.kind', 'vatRates.filters.status'] },
  table: {
    pagination: true,
    columns: [
      { key: 'country', label: 'vatRates.table.country', align: UiAlign.Left, size: UiColumnSize.Grow },
      { key: 'kind', label: 'vatRates.table.kind', align: UiAlign.Left, kind: UiColumnKind.Status },
      { key: 'rate', label: 'vatRates.table.rate', numeric: true, size: UiColumnSize.Compact },
      { key: 'validFrom', label: 'vatRates.table.validFrom', align: UiAlign.Left, kind: UiColumnKind.Date },
      { key: 'actions', actions: true },
    ],
  },
}

export default schema
