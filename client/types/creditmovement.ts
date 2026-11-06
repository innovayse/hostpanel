/**
 * Why a client's credit balance moved, as `Innovayse.Hostpanel.Domain.Billing.Enums.CreditMovementType` serialises.
 *
 * `Program.cs` registers a `JsonStringEnumConverter`, so these arrive as the member names verbatim. All ten members are listed;
 * the history table names each by its `client.credit.movement.*` sentence.
 *
 * @module types/creditmovement
 */

/** The kinds of movement the credit history records. */
export enum CreditMovement {
  /** Staff added credit. */
  AdminAdd = 'AdminAdd',
  /** Staff took credit off the balance. */
  AdminDeduct = 'AdminDeduct',
  /** Credit was applied to an invoice. */
  InvoiceApplied = 'InvoiceApplied',
  /** Credit an invoice held went back to the balance. */
  InvoiceReturned = 'InvoiceReturned',
  /** The renewal sweep applied credit to a renewal invoice. */
  RenewalAutoApplied = 'RenewalAutoApplied',
  /** A refund was paid back as credit. */
  RefundToCredit = 'RefundToCredit',
  /** A paid top-up invoice put its amount on the balance. */
  TopUp = 'TopUp',
  /** A refund of a paid top-up took the refunded amount off the balance. */
  TopUpRefunded = 'TopUpRefunded',
  /** A payment beyond an invoice, or one that could not be recorded on it, went to the balance. */
  PaymentSurplus = 'PaymentSurplus',
  /** A legacy ledger entry was deleted and its credit move undone. */
  TransactionReversed = 'TransactionReversed'
}
