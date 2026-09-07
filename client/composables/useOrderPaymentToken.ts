/**
 * Remembers the payment token an order was placed with, so the payer can still prove the order
 * is theirs after the bank has sent them back.
 *
 * A guest checkout has no account, so the order's payment endpoints are authorised by this token
 * rather than by a credential. The Stripe path never needs storage — the token is used a few
 * lines after it arrives. The hosted-gateway path does: the browser leaves for the bank and comes
 * back to `/payment/result` as a fresh navigation, with nothing but `?order=` in hand.
 *
 * `localStorage`, not `sessionStorage`: a 3-D Secure step can return the payer into a different
 * tab or window, which starts a new session and would lose a token kept in session storage. The
 * token is scoped to one order and dropped as soon as that order's payment has been verified.
 *
 * @module composables/useOrderPaymentToken
 */

/** Prefix for the per-order localStorage keys. */
const STORAGE_PREFIX = 'order-payment-token:'

/**
 * Builds the storage key for one order.
 *
 * @param orderId - The order the token belongs to.
 * @returns The localStorage key.
 */
const keyFor = (orderId: string | number): string => `${STORAGE_PREFIX}${orderId}`

/**
 * Reads, writes and clears the payment token for an order.
 *
 * Every function is a no-op on the server, where `localStorage` does not exist; `recall` returns
 * `undefined` there, which the callers already treat as "no token" and refuse on.
 *
 * @returns The remember/recall/forget trio.
 */
export const useOrderPaymentToken = () => {
  /**
   * Stores the token for an order.
   *
   * @param orderId - The order the token belongs to.
   * @param token - The payment token returned when the order was placed.
   */
  const remember = (orderId: string | number, token: string): void => {
    if (!import.meta.client) return
    localStorage.setItem(keyFor(orderId), token)
  }

  /**
   * Reads the stored token for an order.
   *
   * @param orderId - The order to look up.
   * @returns The token, or `undefined` when none was stored.
   */
  const recall = (orderId: string | number): string | undefined => {
    if (!import.meta.client) return undefined
    return localStorage.getItem(keyFor(orderId)) ?? undefined
  }

  /**
   * Drops the stored token for an order.
   *
   * @param orderId - The order whose token is no longer needed.
   */
  const forget = (orderId: string | number): void => {
    if (!import.meta.client) return
    localStorage.removeItem(keyFor(orderId))
  }

  return { remember, recall, forget }
}
