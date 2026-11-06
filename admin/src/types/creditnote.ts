/**
 * Credit notes as staff read and write them (`/api/billing/credit-notes`, `CreditNoteDto`, `CreditNoteDetailDto`).
 *
 * A credit note is not an invoice credit: invoice credit is balance a client spends on an invoice, a credit note is a document
 * that reverses part of an invoice that was already paid. The enum values are the member names the API serialises.
 */

/** Lifecycle state of a credit note, as `CreditNoteStatus` serialises. */
export enum CreditNoteStatus {
  /** Being prepared: no number yet, nothing has moved. */
  Draft = 'Draft',
  /** Numbered; its value has not been given to the client yet. */
  Issued = 'Issued',
  /** Its value was given to the client. */
  Applied = 'Applied',
  /** Cancelled. It keeps its number and stays on record; a note is never deleted. */
  Voided = 'Voided',
}

/** How a credit note gives its value back, as `CreditNoteMethod` serialises. */
export enum CreditNoteMethod {
  /** The value goes on the client's credit balance and shows in the credit history. */
  AccountCredit = 'AccountCredit',
  /** The value is paid back through the gateway that took the money, as one refund of the invoice. */
  GatewayRefund = 'GatewayRefund',
}

/** A credit note as a list row or the top of its page. */
export interface CreditNote {
  /** Primary key. */
  id: number
  /** The number such as `CN-0001`; null for a draft. */
  number: string | null
  /** Lifecycle state. */
  status: CreditNoteStatus
  /** How the value goes back to the client. */
  method: CreditNoteMethod
  /** The client the note is issued to. */
  clientId: number
  /** Display name of that client. */
  clientName: string
  /** The invoice the note credits. */
  invoiceId: number
  /** ISO 4217 code every amount is in. */
  currency: string
  /** Sum of the lines before tax. */
  subTotal: number
  /** Tax percentage, copied from the invoice. */
  taxRate: number
  /** Tax on the sub total. */
  tax: number
  /** What the note credits in all. */
  total: number
  /** Why the note was issued; printed on the note, so the client reads it. */
  reason: string
  /** The gateway reference of the refund a gateway refund note paid out; null otherwise. */
  refundReference: string | null
  /** ISO 8601 moment the note was created. */
  createdAt: string
  /** ISO 8601 moment it got its number; null for a draft. */
  issuedAt: string | null
  /** ISO 8601 moment its value was given to the client; null until then. */
  appliedAt: string | null
  /** ISO 8601 moment it was voided; null while it stands. */
  voidedAt: string | null
  /** Why it was voided; null while it stands. */
  voidReason: string | null
}

/** One line of a credit note. */
export interface CreditNoteItem {
  /** Primary key. */
  id: number
  /** What is credited. */
  description: string
  /** Credited price per unit, before tax. */
  unitPrice: number
  /** Number of units credited. */
  quantity: number
  /** Line total before tax. */
  amount: number
}

/** A credit note with its lines (`GET /api/billing/credit-notes/{id}`). */
export interface CreditNoteDetail {
  /** The note itself. */
  note: CreditNote
  /** The lines credited, in their stored order. */
  items: CreditNoteItem[]
}

/** One line staff credit, as the issue request carries it. */
export interface CreditNoteLineInput {
  /** What is credited. */
  description: string
  /** Credited price per unit, before tax. */
  unitPrice: number
  /** Number of units credited. */
  quantity: number
}

/** Body of the issue request (`POST /api/billing/{invoiceId}/credit-notes`). */
export interface IssueCreditNotePayload {
  /** The lines credited, before tax; at least one. */
  items: CreditNoteLineInput[]
  /** Where the value goes. */
  method: CreditNoteMethod
  /** Why the note is issued; the client reads it on the note. */
  reason: string
}

/** Filters of the credit note list. */
export interface CreditNoteFilters {
  /** Only the notes of this invoice. */
  invoiceId?: number
  /** Only the notes of this client. */
  clientId?: number
  /** Only the notes in this state. */
  status?: CreditNoteStatus
}
