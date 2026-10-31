/**
 * Saved payment method - a card or bank account on file for the authenticated client
 * Used in the client account page's Payment Methods tab
 * Returned by GET /api/portal/client/payment-methods (proxying the C# backend's
 * api/me/payment-methods) as {@link PaymentMethodWire}, then mapped to {@link PaymentMethod}
 * by `toPaymentMethod`.
 */

/** The kinds of saved payment method the card gateway reports; a closed set, so an enum. */
export enum PaymentMethodType {
  /** A debit or credit card. */
  Card = 'Card',
  /** A bank account debited directly. */
  BankAccount = 'BankAccount',
  /** Any other method the gateway reports. */
  Other = 'Other'
}

/** One saved method exactly as the API sends it (camelCase). */
export interface PaymentMethodWire {
  /** Gateway payment method ID (e.g. "pm_1AbC"). */
  id: string
  /** The kind of method */
  type: PaymentMethodType
  /** Card network (e.g. "visa"), or the bank name for a bank account */
  brand?: string | null
  /** Last four digits of the card or account number */
  last4?: string | null
  /** Card expiry month (1-12); absent for a bank account */
  expiryMonth?: number | null
  /** Card expiry year (4 digits); absent for a bank account */
  expiryYear?: number | null
  /** Whether this is the client's default method */
  isDefault: boolean
  /** Whether the client agreed that renewals may be charged to this card without them */
  autoCharge?: boolean
}

/** One saved method as the client renders it. */
export interface PaymentMethod {
  /** Gateway payment method ID (e.g. "pm_1AbC"). */
  id: string
  /** The kind of method */
  type: PaymentMethodType
  /** Display label, e.g. "Visa •••• 4242"; falls back to gatewayName when absent */
  description: string
  /** Gateway name shown when there is no description */
  gatewayName: string
  /** Last four digits of the card or account number */
  cardLastFour?: string | null
  /** Formatted card expiry (MM/YY) */
  cardExpiry?: string | null
  /** Card network (e.g. "visa") */
  cardType?: string | null
  /** Bank name, for a bank account payment method */
  bankName?: string | null
  /** Whether this is the client's default method */
  isDefault?: boolean
  /** Whether renewals are charged to this card automatically, by the client's agreement */
  autoCharge?: boolean
}
