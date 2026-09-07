/**
 * Represents a payment gateway configuration.
 * @deprecated Use IntegrationDto from the integrations module instead.
 */
export interface Gateway {
  /** Unique gateway identifier. */
  id: number
  /** Gateway name (e.g. Stripe, PayPal). */
  name: string
  /** Whether this gateway is currently enabled. */
  isEnabled: boolean
  /** Display order in checkout. */
  displayOrder: number
}
