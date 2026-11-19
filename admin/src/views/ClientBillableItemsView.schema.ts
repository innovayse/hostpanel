/**
 * Skeleton schema of the client billable items tab: a header with its two buttons, a toolbar and two tables, the
 * uninvoiced items and the invoiced ones, one under the other.
 *
 * The screen is a composite: a search box with the type filter, then a detail card per table. The heading of the uninvoiced
 * table is made of the item count and the total, so only the invoiced one has a caption in the schema; the pager under the
 * invoiced table is part of it. The floating bulk bar that appears with a selection is not part of the skeleton.
 */
import { SkeletonKind, SkeletonPartKind } from '@/utils/ui/skeletonSchema'
import type { CardsSkeletonPart, CompositeSkeletonSchema, SkeletonTable } from '@/utils/ui/skeletonSchema'
import { UiColumnSize } from '@/utils/ui/table'

/** The uninvoiced items table: selectable, with the row actions. */
export const UNINVOICED_ITEMS_TABLE: SkeletonTable = {
  columns: [
    { key: 'id', label: 'shared.id', size: UiColumnSize.Compact },
    { key: 'description', label: 'shared.description', size: UiColumnSize.Grow },
    { key: 'amount', label: 'shared.amount', numeric: true, size: UiColumnSize.Compact },
    { key: 'actions', label: 'shared.actions', actions: true },
  ],
}

/** The invoiced items table with its pager. */
export const INVOICED_ITEMS_TABLE: SkeletonTable = {
  pagination: true,
  columns: [
    { key: 'id', label: 'shared.id', size: UiColumnSize.Compact },
    { key: 'description', label: 'shared.description', size: UiColumnSize.Grow },
    { key: 'amount', label: 'shared.amount', numeric: true, size: UiColumnSize.Compact },
    { key: 'invoice', label: 'shared.invoice', size: UiColumnSize.Compact },
  ],
}

/** The two table cards, one column. */
export const BILLABLE_ITEMS_CARDS_PART: CardsSkeletonPart = {
  kind: SkeletonPartKind.Cards,
  columns: [
    [
      { key: 'uninvoiced', table: UNINVOICED_ITEMS_TABLE },
      { key: 'invoiced', title: 'clients.clientBillableItems.invoicedItems', table: INVOICED_ITEMS_TABLE },
    ],
  ],
}

/** The client billable items tab. */
const schema: CompositeSkeletonSchema = {
  kind: SkeletonKind.Composite,
  header: { title: 'clients.clientBillableItems.billableItems', actions: 2 },
  parts: [{ kind: SkeletonPartKind.Toolbar, toolbar: { search: true, filters: ['shared.type'] } }, BILLABLE_ITEMS_CARDS_PART],
}

export default schema
