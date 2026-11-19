/**
 * Skeleton schema of the client credit tab: a header over the balance and currency tiles, the card that adjusts the balance
 * and the card of the credit history, one under the other.
 *
 * The screen is a composite: a row of two tiles, a one column form (direction, amount, reason) and a detail card that holds
 * the history table with its pager. `ClientCreditView` draws every block from this data and `UiSkeletonOf` draws the same
 * blocks while the view chunk is downloading.
 */
import { SkeletonKind, SkeletonPartKind } from '@/utils/ui/skeletonSchema'
import type { CardsSkeletonPart, CompositeSkeletonSchema, FormSkeletonPart, SkeletonTable, StatsSkeletonPart } from '@/utils/ui/skeletonSchema'
import { UiColumnSize } from '@/utils/ui/table'

/** The two tiles: the balance and the currency it is held in. */
export const CREDIT_STATS_PART: StatsSkeletonPart = {
  kind: SkeletonPartKind.Stats,
  columns: 2,
  stats: [
    { key: 'balance', label: 'clients.clientCredit.balance' },
    { key: 'currency', label: 'clients.clientCredit.currency' },
  ],
}

/** The card that adds credit or takes it off: direction, amount and reason, one under the other. */
export const CREDIT_FORM_PART: FormSkeletonPart = {
  kind: SkeletonPartKind.Form,
  columns: [
    [
      {
        key: 'adjust',
        title: 'clients.clientCredit.adjustTitle',
        columns: 1,
        fields: [
          { key: 'direction', label: 'clients.clientCredit.direction' },
          { key: 'amount', label: 'shared.amount' },
          { key: 'reason', label: 'clients.clientCredit.reason' },
        ],
      },
    ],
  ],
}

/** The history table: the movements of the balance with the pager under it. */
export const CREDIT_HISTORY_TABLE: SkeletonTable = {
  pagination: true,
  columns: [
    { key: 'date', label: 'shared.date', size: UiColumnSize.Compact },
    { key: 'type', label: 'shared.type' },
    { key: 'description', label: 'shared.description', size: UiColumnSize.Grow },
    { key: 'invoice', label: 'shared.invoice', size: UiColumnSize.Compact },
    { key: 'change', label: 'clients.clientCredit.change', numeric: true, size: UiColumnSize.Compact },
    { key: 'balance', label: 'shared.balance', numeric: true, size: UiColumnSize.Compact },
  ],
}

/** The card that holds the history table. */
export const CREDIT_HISTORY_PART: CardsSkeletonPart = {
  kind: SkeletonPartKind.Cards,
  columns: [[{ key: 'history', title: 'clients.clientCredit.history', table: CREDIT_HISTORY_TABLE }]],
}

/** The client credit tab. */
const schema: CompositeSkeletonSchema = {
  kind: SkeletonKind.Composite,
  header: { title: 'clients.clientCredit.credit' },
  parts: [CREDIT_STATS_PART, CREDIT_FORM_PART, CREDIT_HISTORY_PART],
}

export default schema
