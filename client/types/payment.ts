/**
 * Saved payment method - a card on file for the authenticated client
 * Used in the client account page's Payment Methods tab
 * Returned by GET /api/portal/client/payment-methods (proxying the C# backend's
 * api/me/payment-methods) as {@link PaymentMethodWire}, then mapped to {@link PaymentMethod}
 * by `toPaymentMethod`.
 */

/** The kinds of saved payment method the portal shows; a closed set, so an enum. */
export enum PaymentMethodType {
  /** A debit or credit card, or a method the gateway reports as card-like. */
  Card = 'Card'
}

/** One saved method exactly as the API sends it (camelCase). */
export interface PaymentMethodWire {
  /** The gateway module that holds the method (e.g. "stripe"). */
  gatewayModuleId: string
  /** Gateway payment method ID (e.g. "pm_1AbC"). */
  id: string
  /** Card network (e.g. "visa"); absent when the gateway does not report one */
  brand?: string | null
  /** Last four digits of the card number; absent when the gateway does not report them */
  last4?: string | null
  /** Card expiry month (1-12) */
  expiryMonth?: number | null
  /** Card expiry year (4 digits) */
  expiryYear?: number | null
  /** Whether this is the client's default method */
  isDefault: boolean
  /** Whether the client agreed that renewals may be charged to this card without them */
  autoCharge?: boolean
}

/** One saved method as the client renders it. */
export interface PaymentMethod {
  /** The gateway module that holds the method; every call about the method names it. */
  gatewayModuleId: string
  /** Gateway payment method ID (e.g. "pm_1AbC"). */
  id: string
  /** The kind of method */
  type: PaymentMethodType
  /** Display label, e.g. "Visa •••• 4242"; falls back to gatewayName when absent */
  description: string
  /** Gateway name shown when there is no description */
  gatewayName: string
  /** Last four digits of the card number */
  cardLastFour?: string | null
  /** Formatted card expiry (MM/YY) */
  cardExpiry?: string | null
  /** Card network (e.g. "visa") */
  cardType?: string | null
  /** Whether this is the client's default method */
  isDefault?: boolean
  /** Whether renewals are charged to this card automatically, by the client's agreement */
  autoCharge?: boolean
}
