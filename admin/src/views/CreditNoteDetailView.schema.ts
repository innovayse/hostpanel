/**
 * Skeleton schema of the credit note screen: a header whose title and subtitle are made of the number and the reason
 * of the note, the card of its facts and the card of its lines.
 *
 * The state badge has no caption of its own, and the refund reference, the void line and the totals exist only for
 * some notes, so the facts card lists the captions every note has and the lines card holds the table. The real screen
 * puts its bodies in the `card-details` and `card-items` slots of `UiDetailCards`.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { DetailSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiAlign, UiColumnSize } from '@/utils/ui/table'

/** What the credit note screen is made of. */
const schema: DetailSkeletonSchema = {
  kind: SkeletonKind.Detail,
  header: { subtitleFromData: true, crumbs: true, actions: 1 },
  columns: [
    [
      {
        key: 'details',
        rows: [
          'billing.creditNotes.invoice',
          'billing.creditNotes.client',
          'billing.creditNotes.methodLabel',
          'billing.creditNotes.reason',
          'billing.creditNotes.issuedAt',
        ],
      },
      {
        key: 'items',
        table: {
          columns: [
            { key: 'description', label: 'billing.creditNotes.description', size: UiColumnSize.Grow },
            { key: 'quantity', label: 'billing.creditNotes.quantity', align: UiAlign.Right, numeric: true },
            { key: 'unitPrice', label: 'billing.creditNotes.unitPrice', align: UiAlign.Right, numeric: true },
            { key: 'amount', label: 'billing.creditNotes.amount', align: UiAlign.Right, numeric: true },
          ],
          rows: 3,
        },
      },
    ],
  ],
}

export default schema
