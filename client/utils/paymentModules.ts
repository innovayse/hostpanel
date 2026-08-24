/**
 * Module ids for the payment methods built into checkout rather than loaded as a plugin.
 * Mirrors the backend's `Innovayse.API.Billing.BuiltInPaymentModules` — the two must stay
 * in lockstep, since the API returns these exact strings in the `module` field of
 * `GET /api/payment-methods`.
 */

/** Module id for the built-in Stripe card payment method. */
export const PAYMENT_MODULE_STRIPE = 'stripe'

/** Module id for the built-in manual bank transfer payment method. */
export const PAYMENT_MODULE_BANK_TRANSFER = 'bank_transfer'
