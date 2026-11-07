/**
 * The "save this payment method for automatic renewals" choice a payer made at checkout.
 *
 * @module types/pendingcheckoutsave
 */

/** What checkout remembers across the redirect to the bank so the return page can act on the choice once. */
export interface PendingCheckoutSave {
  /** Gateway module the order was paid through, and the one the method is to be stored at. */
  gatewayModuleId: string
  /** The account that made the choice (its email, lower-cased); no other account may act on it. */
  owner: string
  /** Epoch milliseconds after which the choice is no longer honoured. */
  expiresAt: number
}
