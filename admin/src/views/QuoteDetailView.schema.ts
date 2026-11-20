/**
 * Skeleton schema of the quote screen: the header (its title is made of the quote number), the card of the general data,
 * the line items and the card of the three notes, in that order.
 *
 * The screen is a composite of three blocks of cards. `QuoteDetailView` puts the real body of each in the `card-general`,
 * `card-items` and `card-notes` slots of `UiDetailCards` (the line items card is the one `quoteSchemaParts` shares with the
 * create screen), and a skeleton shows the captions over bars.
 */
import { QUOTE_ITEMS_PART } from '@/lib/quoteSchemaParts'
import { SkeletonKind, SkeletonPartKind } from '@/utils/ui/skeletonSchema'
import type { CardsSkeletonPart, CompositeSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** The general data card: subject, stage, the date the quote was created and the date it is valid until. */
export const QUOTE_GENERAL_PART: CardsSkeletonPart = {
  kind: SkeletonPartKind.Cards,
  columns: [
    [
      {
        key: 'general',
        title: 'billing.quoteDetail.generalInformation',
        rows: ['shared.subject', 'shared.stage', 'billing.quoteDetail.dateCreated', 'shared.validUntil'],
      },
    ],
  ],
}

/** The card of the three notes: proposal text, customer notes and admin only notes. */
export const QUOTE_NOTES_PART: CardsSkeletonPart = {
  kind: SkeletonPartKind.Cards,
  columns: [
    [
      {
        key: 'notes',
        title: 'shared.notes',
        rows: ['billing.quoteDetail.proposalText', 'billing.quoteDetail.customerNotes', 'billing.quoteDetail.adminOnlyNotes'],
      },
    ],
  ],
}

/** What the quote screen is made of. */
const schema: CompositeSkeletonSchema = {
  kind: SkeletonKind.Composite,
  header: { backLabel: 'shared.quotes', backMobileOnly: true, actions: 4 },
  parts: [QUOTE_GENERAL_PART, QUOTE_ITEMS_PART, QUOTE_NOTES_PART],
}

export default schema
