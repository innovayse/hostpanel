/**
 * The block of the quote screens that both of them share, as skeleton schema data: the card of line items.
 *
 * The create screen and the quote screen both hold the lines in `QuoteItemsEditor`, so its table head and its card live here
 * once and each screen's schema places the card between its own blocks. The editor draws its head from the same columns, and
 * `UiSkeletonOf` draws the placeholder of the same card. Pure data: every text is a locale key.
 */
import { SkeletonPartKind } from '@/utils/ui/skeletonSchema'
import type { CardsSkeletonPart, SkeletonTable } from '@/utils/ui/skeletonSchema'
import { UiAlign, UiCardRole, UiColumnSize } from '@/utils/ui/table'

/** The table of line items: quantity, description, unit price, discount, tax flag, total and the row actions. */
export const QUOTE_ITEMS_TABLE: SkeletonTable = {
  columns: [
    { key: 'quantity', label: 'shared.qty', cardRole: UiCardRole.Control },
    { key: 'description', label: 'shared.description', size: UiColumnSize.Grow, cardRole: UiCardRole.Control },
    { key: 'unitPrice', label: 'billing.quoteDetail.unitPrice', cardRole: UiCardRole.Control },
    { key: 'discount', label: 'billing.quoteDetail.discount', cardRole: UiCardRole.Control },
    { key: 'taxed', label: 'shared.taxed', align: UiAlign.Center, size: UiColumnSize.Compact },
    { key: 'total', label: 'shared.total', align: UiAlign.Right, numeric: true, size: UiColumnSize.Compact },
    { key: 'actions', label: 'shared.actions', actions: true },
  ],
  rows: 3,
}

/** The card of line items: its title, the table and the row with the Add item button. */
export const QUOTE_ITEMS_PART: CardsSkeletonPart = {
  kind: SkeletonPartKind.Cards,
  columns: [[{ key: 'items', title: 'shared.lineItems', table: QUOTE_ITEMS_TABLE, actions: 1 }]],
}
