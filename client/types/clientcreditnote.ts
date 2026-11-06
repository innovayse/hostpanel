/**
 * One credit note issued to the signed-in client, as `GET /api/portal/client/credit-notes` returns it (`MyCreditNoteDto`).
 *
 * A credit note is not invoice credit: invoice credit is balance the client spends on an invoice, a credit note is a numbered document
 * that reverses part of an invoice that was already paid. Drafts are never sent to a client.
 *
 * @module types/clientcreditnote
 */

/** Where a credit note stands, as the backend names it. A client sees a note that was issued, applied or voided. */
export enum ClientCreditNoteStatus {
  /** Numbered, and its value not given yet. */
  Issued = 'Issued',
  /** Its value was given to the client. */
  Applied = 'Applied',
  /** Cancelled; it keeps its number and stays on record. */
  Voided = 'Voided'
}

/** How the value of a credit note went back to the client, as the backend names it. */
export enum ClientCreditNoteMethod {
  /** The value went on the client's credit balance. */
  AccountCredit = 'AccountCredit',
  /** The value was paid back to the payer through the gateway. */
  GatewayRefund = 'GatewayRefund'
}

/** A credit note issued to the client. */
export interface ClientCreditNote {
  /** Credit note id. */
  id: number
  /** The number, such as `CN-0001`. */
  number: string
  /** Where the note stands. */
  status: `${ClientCreditNoteStatus}`
  /** How the value went back to the client. */
  method: `${ClientCreditNoteMethod}`
  /** The invoice the note credits. */
  invoiceId: number
  /** ISO 4217 code every amount is in. */
  currency: string
  /** Sum of the lines before tax. */
  subTotal: number
  /** Tax percentage. */
  taxRate: number
  /** Tax on the sub total. */
  tax: number
  /** What the note credits in all. */
  total: number
  /** Why the note was issued. */
  reason: string
  /** When the note was issued (UTC, ISO 8601). */
  issuedAt: string
  /** When it was voided (UTC, ISO 8601); null while it stands. */
  voidedAt: string | null
}
