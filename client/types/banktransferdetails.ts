/**
 * What a client needs to pay one invoice by manual bank transfer, as
 * `POST` and `GET /api/portal/client/invoices/:id/bank-transfer` return it.
 *
 * Field names are `BankTransferDetailsDto`'s own. The reference is built from the invoice
 * number alone, so there is no client id in it or beside it.
 *
 * @module types/banktransferdetails
 */

/** The operator's bank details and the reference to write on the transfer. */
export interface BankTransferDetails {
  /** The invoice the transfer pays. */
  invoiceId: number
  /** The reference to write on the transfer, so the payment can be matched to the invoice. */
  reference: string
  /** What the transfer must be for, in `currency`. */
  amountDue: number
  /** ISO 4217 code the invoice is billed in. */
  currency: string
  /** The account holder; null when the operator left it empty. */
  accountName: string | null
  /** The IBAN; null when the operator left it empty. */
  iban: string | null
  /** The SWIFT/BIC code; null when the operator left it empty. */
  swift: string | null
  /** The bank's name; null when the operator left it empty. */
  bankName: string | null
  /** Free text from the operator; null when the operator left it empty. */
  instructions: string | null
}
