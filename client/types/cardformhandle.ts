/**
 * What the card form component exposes to the page or tab that mounts it.
 *
 * @module types/cardformhandle
 */

/** The card form below a method list, as far as its parent needs to drive it. */
export interface CardFormHandle {
  /** Confirms the card payment with a PaymentIntent client secret once the order exists. */
  confirmPayment: (clientSecret: string) => Promise<{ id: string }>
  /** Confirms only the saving of the card with a SetupIntent client secret, without a payment. */
  confirmSetup: (clientSecret: string) => Promise<{ id: string }>
  /** Whether the provider's card field is mounted and ready for input. */
  ready: boolean
}
