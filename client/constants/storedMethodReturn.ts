/**
 * Names the saved-method flow shares between the page that leaves and the page that returns.
 *
 * @module constants/storedMethodReturn
 */

/** Query parameter that marks the return from a gateway's save page. */
export const STORED_METHOD_RETURN_QUERY = 'storedReturn'

/** Browser session-storage key of the pending save session. */
export const PENDING_STORED_SESSION_KEY = 'pendingStoredSession'

/** Prefix of the session-storage key that holds, per order, the "save for renewals" choice made at checkout. */
export const PENDING_CHECKOUT_SAVE_KEY_PREFIX = 'checkoutSaveForRenewals:'

/** How long the checkout choice survives the trip to the bank: longer than a bank session, short enough not to linger. */
export const PENDING_CHECKOUT_SAVE_TTL_MS = 60 * 60 * 1000
