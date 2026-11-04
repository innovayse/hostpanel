/**
 * A gateway module a payment method can be saved with.
 *
 * @module types/savedmethodmodule
 */

/** One entry of `GET /me/payment-methods/modules`. */
export interface SavedMethodModule {
  /** The gateway module id, the value every saved-method call names it by. */
  module: string
  /** The name shown to the client. */
  displayName: string
}
