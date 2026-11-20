/**
 * Skeleton schema of the currencies screen: a header with its three buttons over the exchange rate provider card and the
 * table of currencies.
 *
 * The screen is a composite: the provider card (its heading, its description line and the caption of the select row) and
 * the currencies table. `CurrenciesView` draws the card as a placeholder while the settings load and heads its table from
 * these columns; `UiSkeletonOf` draws both while the view chunk is downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind, SkeletonPartKind } from '@/utils/ui/skeletonSchema'
import type { CardsSkeletonPart, CompositeSkeletonSchema, SkeletonTable } from '@/utils/ui/skeletonSchema'
import { UiColumnSize } from '@/utils/ui/table'

/** The exchange rate provider card: its title, its description and the caption of the row that holds the select. */
export const CURRENCY_PROVIDER_PART: CardsSkeletonPart = {
  kind: SkeletonPartKind.Cards,
  columns: [[{ key: 'provider', title: 'currencies.rateProvider.title', lines: 1, rows: ['currencies.rateProvider.label'] }]],
}

/** The currencies table. */
export const CURRENCIES_TABLE: SkeletonTable = {
  columns: [
    { key: 'code', label: 'currencies.table.code', align: 'left' },
    { key: 'numeric', label: 'currencies.table.numeric', align: 'left' },
    { key: 'format', label: 'currencies.table.format', align: 'left' },
    { key: 'decimals', label: 'currencies.table.decimals', numeric: true },
    { key: 'rateToBase', label: 'currencies.table.rateToBase', numeric: true },
    { key: 'base', label: 'currencies.table.base', align: 'left' },
    { key: 'enabled', label: 'currencies.table.enabled', align: 'left', size: UiColumnSize.Compact },
    { key: 'actions', label: 'currencies.table.actions', actions: true },
  ],
}

/** The currencies screen. */
const schema: CompositeSkeletonSchema = {
  kind: SkeletonKind.Composite,
  header: { title: navTitleKey(AppRoute.SettingsCurrencies), subtitle: 'currencies.description', actions: 3, primaryAction: 2 },
  parts: [CURRENCY_PROVIDER_PART, { kind: SkeletonPartKind.Table, table: CURRENCIES_TABLE }],
}

export default schema
