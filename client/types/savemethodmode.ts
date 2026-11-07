/**
 * How the payer enters a payment method to store, as the API names it.
 *
 * @module types/savemethodmode
 */

/** The two ways a save session collects the method; the value is the API's own. */
export enum SaveMethodMode {
  /** The method is entered in a form the portal hosts, using a client token. */
  EmbeddedToken = 'EmbeddedToken',
  /** The payer is sent to the gateway's page and returns afterwards. */
  Redirect = 'Redirect'
}
