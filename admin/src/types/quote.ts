/**
 * A quote, its lines, its list row and the stages it moves through.
 */

/** Quote stage values. */
export const QuoteStage = {
  Draft: 'Draft',
  Delivered: 'Delivered',
  OnHold: 'OnHold',
  Accepted: 'Accepted',
  Lost: 'Lost',
  Expired: 'Expired',
  Dead: 'Dead',
} as const

/** One of the {@link QuoteStage} values. */
export type QuoteStage = (typeof QuoteStage)[keyof typeof QuoteStage]

/** Quote list item returned by the list endpoint. */
export interface QuoteListItem {
  /** Unique quote identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Display name of the owning client; every quote has one, since its client cannot be deleted. */
  clientName: string
  /** Quote subject line. */
  subject: string
  /** ISO 8601 creation date. */
  dateCreated: string
  /** ISO 8601 validity deadline. */
  validUntil: string
  /** Total quoted amount. */
  total: number
  /** ISO 4217 code the quote's amounts are in. */
  currency: string
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
  /** Display name of the owning client; every quote has one, since its client cannot be deleted. */
  clientName: string
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
  /** ISO 4217 code the quote's amounts are in. */
  currency: string
  /** Proposal/sales text sent to the client. */
  proposalText?: string
  /** Notes visible to the customer. */
  customerNotes?: string
  /** Internal admin-only notes. */
  adminNotes?: string
  /** Line items on the quote. */
  items: QuoteItem[]
}

/** One line of a quote being created. */
export interface QuoteItemInput {
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
}

/** One line of a quote being updated; `id` is absent for a new line. */
export interface QuoteItemUpdate extends QuoteItemInput {
  /** Existing line item id. */
  id?: number
}

/** A quote line as the editor holds it: the wire fields plus a client-side key that survives edits. */
export interface QuoteLineDraft extends QuoteItemUpdate {
  /** Stable key of the row for list rendering; never sent to the API. */
  key: number
}

/** Fields the quote create and update bodies share. */
export interface QuoteFields {
  /** Quote subject line. */
  subject: string
  /** Stage of the quote. */
  stage: QuoteStage
  /** ISO 8601 validity deadline. */
  validUntil: string
  /** Proposal/sales text sent to the client. */
  proposalText?: string
  /** Notes visible to the customer. */
  customerNotes?: string
  /** Internal admin-only notes. */
  adminNotes?: string
}

/** Body of `POST /billing/quotes`. */
export interface CreateQuotePayload extends QuoteFields {
  /** The client the quote is for. */
  clientId: number
  /** Line items. */
  items: QuoteItemInput[]
}

/** Body of `PUT /quotes/{id}`. */
export interface UpdateQuotePayload extends QuoteFields {
  /** Line items. */
  items: QuoteItemUpdate[]
}
