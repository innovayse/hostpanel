/**
 * One manual bank transfer that waits for staff to confirm or reject it, as the pending transfers list returns it
 * (`PendingBankTransferDto`).
 */

/** A waiting bank transfer: the invoice it pays, the client, the amount and the reference the client was told to write. */
export interface PendingBankTransfer {
  /** The invoice the transfer pays. */
  invoiceId: number
  /** The reference the client was told to write on the transfer, built from the invoice number. */
  reference: string
  /** The invoiced client. */
  clientId: number
  /** Full name of the invoiced client. */
  clientName: string
  /** Invoice total, in `currency`. */
  total: number
  /** What the transfer must be for, in `currency`. */
  amountDue: number
  /** ISO 4217 code the invoice is billed in. */
  currency: string
  /** ISO 8601 payment due date of the invoice. */
  dueDate: string
  /** ISO 8601 time the client chose the transfer. */
  requestedAt: string
}
