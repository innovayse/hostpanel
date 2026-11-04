/**
 * The family a payment gateway belongs to, which decides its icon and its short description.
 *
 * @module types/paymentgatewaykind
 */

/** How a gateway takes the money. */
export enum PaymentGatewayKind {
  /** A manual transfer to a bank account. */
  BankTransfer = 'bank_transfer',
  /** Any gateway this list does not know by name. */
  Other = 'other'
}
