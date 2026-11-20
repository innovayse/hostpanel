/**
 * Skeleton schema of the create quote screen: a header over the general information card, the card of line items and the
 * card of notes, in that order.
 *
 * The screen is a composite: two forms with the line items card between them, so the items sit before the notes as the
 * operator reads a quote. `AddQuoteView` draws every block from this data, the line items card holding `QuoteItemsEditor`,
 * and `UiSkeletonOf` draws the same blocks while the view chunk is downloading.
 */
import { QUOTE_ITEMS_PART } from '@/lib/quoteSchemaParts'
import { SkeletonFieldKind, SkeletonKind, SkeletonPartKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { CompositeSkeletonSchema, FormSkeletonPart } from '@/utils/ui/skeletonSchema'

/** The general information card: subject, stage, client and the date the quote is valid until. */
export const QUOTE_GENERAL_FORM_PART: FormSkeletonPart = {
  kind: SkeletonPartKind.Form,
  columns: [
    [
      {
        key: 'general',
        title: 'billing.addQuote.generalInformation',
        fields: [
          { key: 'subject', label: 'shared.subject' },
          { key: 'stage', label: 'shared.stage' },
          { key: 'clientId', label: 'shared.client' },
          { key: 'validUntil', label: 'shared.validUntil' },
        ],
      },
    ],
  ],
}

/** The card of the three notes: proposal text, customer notes and admin only notes. */
export const QUOTE_NOTES_FORM_PART: FormSkeletonPart = {
  kind: SkeletonPartKind.Form,
  columns: [
    [
      {
        key: 'notes',
        columns: 1,
        fields: [
          { key: 'proposalText', label: 'billing.addQuote.proposalText', kind: SkeletonFieldKind.Textarea, span: SkeletonSpan.Full },
          { key: 'customerNotes', label: 'billing.addQuote.customerNotes', kind: SkeletonFieldKind.Textarea, span: SkeletonSpan.Full },
          { key: 'adminNotes', label: 'billing.addQuote.adminOnlyNotes', kind: SkeletonFieldKind.Textarea, span: SkeletonSpan.Full },
        ],
      },
    ],
  ],
}

/** What the create quote screen is made of. */
const schema: CompositeSkeletonSchema = {
  kind: SkeletonKind.Composite,
  header: {
    title: 'billing.addQuote.createQuote',
    subtitle: 'billing.addQuote.createANewQuoteFor',
  },
  parts: [QUOTE_GENERAL_FORM_PART, QUOTE_ITEMS_PART, QUOTE_NOTES_FORM_PART],
}

export default schema
