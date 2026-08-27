/**
 * Saved payment method - a card or bank account on file for the authenticated client
 * Used in the client account page's Payment Methods tab
 * Returned by GET /api/portal/client/payment-methods (proxying the C# backend's Stripe-backed
 * api/me/payment-methods)
 */
export interface PaymentMethod {
  /** Stripe PaymentMethod ID (e.g. "pm_1AbC...") */
  id: string
  /** Stripe's payment method type ("card" or "us_bank_account") */
  type: string
  /** Display label; falls back to gateway_name when absent */
  description: string
  /** Gateway name shown when there is no description */
  gateway_name: string
  /** WHMCS sub-contact this card's billing address is assigned to, if any */
  contact_id?: number
  /** Last four digits of the card or account number */
  card_last_four?: string | null
  /** Formatted card expiry (MM/YY) */
  card_expiry?: string | null
  /** Card network (e.g. "visa") */
  card_type?: string | null
  /** Bank name, for a bank account payment method */
  bank_name?: string | null
}
