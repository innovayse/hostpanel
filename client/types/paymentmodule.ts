/**
 * Module ids of the payment methods the portal knows by name.
 *
 * `BankTransfer` is built into checkout and mirrors the backend's built-in module id, since the
 * API returns this exact string in the `module` field of `GET /api/payment-methods`. Every other
 * payment method comes from an installed gateway plugin and stays a plain string on the wire.
 */
export enum PaymentModule {
  /** The built-in manual bank transfer payment method. */
  BankTransfer = 'bank_transfer'
}
