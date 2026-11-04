/**
 * How a gateway module wants a payment method to be saved.
 *
 * @module types/savemethodmode
 */

/** The two ways a save session ends in the browser; the values are the API's strings. */
export enum SaveMethodMode {
  /** The browser confirms the method itself with the token the session carries (Stripe's SetupIntent client secret). */
  EmbeddedToken = 'EmbeddedToken',
  /** The payer is sent to the gateway's hosted page and comes back to the portal. */
  Redirect = 'Redirect'
}
