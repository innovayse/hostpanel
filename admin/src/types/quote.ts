/**
 * A quote, its lines, its list row and the stages it moves through.
 */

/** Quote stage values. */
export type QuoteStage = 'Draft' | 'Delivered' | 'OnHold' | 'Accepted' | 'Lost' | 'Expired' | 'Dead'

/** Quote list item returned by the list endpoint. */
export interface QuoteListItem {
  /** Unique quote identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Display name of the owning client. */
  clientName: string
  /** Quote subject line. */
  subject: string
  /** ISO 8601 creation date. */
  dateCreated: string
  /** ISO 8601 validity deadline. */
  validUntil: string
  /** Total quoted amount. */
  total: number
  /** Current stage of the quote. */
  stage: QuoteStage
}

/** Quote line item. */
export interface QuoteItem {
  /** Unique line item identifier. */
  id: number
  /** Number of units. */
  quantity: number
  /** Human-readable charge description. */
  description: string
  /** Price per unit. */
  unitPrice: number
  /** Discount percentage (0-100). */
  discountPercent: number
  /** Whether the item is subject to tax. */
  taxed: boolean
  /** Computed line total. */
  amount: number
}

/** Full quote detail returned by GET /api/quotes/{id}. */
export interface Quote {
  /** Unique quote identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Quote subject line. */
  subject: string
  /** Current stage of the quote. */
  stage: QuoteStage
  /** ISO 8601 creation date. */
  dateCreated: string
  /** ISO 8601 validity deadline. */
  validUntil: string
  /** Sub-total before tax. */
  subTotal: number
  /** Total quoted amount. */
  total: number
  /** Proposal/sales text sent to the client. */
  proposalText?: string
  /** Notes visible to the customer. */
  customerNotes?: string
  /** Internal admin-only notes. */
  adminNotes?: string
  /** Line items on the quote. */
  items: QuoteItem[]
}
