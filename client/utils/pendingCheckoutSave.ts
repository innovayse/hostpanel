/**
 * Remembers, per order, that the payer ticked "save this payment method for automatic renewals"
 * at checkout, across the trip to the bank.
 *
 * It uses the same mechanism as the order payment token: session storage, one entry per order,
 * which survives the redirect and the return in the same tab. The entry is consumed once:
 * {@link consumeCheckoutSave} removes it as it reads it, so a reload of the return page cannot start
 * a second session.
 *
 * Every entry names the account that made the choice (its email, lower-cased). On a shared browser
 * another account may open the return page; it must not start a session on a consent it never gave,
 * so {@link consumeCheckoutSave} drops an entry whose owner is not the current account, and
 * {@link clearCheckoutSaves} removes every entry when somebody signs out.
 *
 * @module utils/pendingCheckoutSave
 */

import { PENDING_CHECKOUT_SAVE_KEY_PREFIX, PENDING_CHECKOUT_SAVE_TTL_MS } from '~/constants/storedMethodReturn'
import type { PendingCheckoutSave } from '~/types/pendingcheckoutsave'

/**
 * Builds the storage key of one order.
 *
 * @param orderId - The order the choice belongs to.
 * @returns The storage key.
 */
const keyFor = (orderId: string | number): string => `${PENDING_CHECKOUT_SAVE_KEY_PREFIX}${orderId}`

/**
 * Whether storage exists here; it does not on the server.
 *
 * @returns True in a browser.
 */
const hasStorage = (): boolean => typeof window !== 'undefined' && typeof window.sessionStorage !== 'undefined'

/**
 * Normalises an account identity to the form stored in an entry.
 *
 * @param owner - The signed-in account's email, or null when it is not known.
 * @returns The comparable form, or null when there is no usable identity.
 */
const ownerKey = (owner: string | null | undefined): string | null => {
  const key = (owner ?? '').trim().toLowerCase()
  return key === '' ? null : key
}

/**
 * Stores the choice for an order.
 *
 * Nothing is stored when the account is unknown: an entry no account can claim would only be dropped later.
 *
 * @param orderId - The order about to be paid at the bank.
 * @param gatewayModuleId - The gateway module the order is paid through.
 * @param owner - The signed-in account's email, the only account allowed to act on the choice.
 */
export const rememberCheckoutSave = (orderId: string | number, gatewayModuleId: string, owner: string | null | undefined): void => {
  const key = ownerKey(owner)
  if (!hasStorage() || key === null) {
    return
  }
  const entry: PendingCheckoutSave = { gatewayModuleId, owner: key, expiresAt: Date.now() + PENDING_CHECKOUT_SAVE_TTL_MS }
  window.sessionStorage.setItem(keyFor(orderId), JSON.stringify(entry))
}

/**
 * Drops the choice for an order, for instance when the payment failed or the payer changed their mind.
 *
 * @param orderId - The order whose choice is no longer wanted.
 */
export const forgetCheckoutSave = (orderId: string | number): void => {
  if (!hasStorage()) {
    return
  }
  window.sessionStorage.removeItem(keyFor(orderId))
}

/**
 * Removes every remembered choice, of every order and account, for instance on sign-out.
 */
export const clearCheckoutSaves = (): void => {
  if (!hasStorage()) {
    return
  }
  const doomed: string[] = []
  for (let i = 0; i < window.sessionStorage.length; i += 1) {
    const name = window.sessionStorage.key(i)
    if (name !== null && name.startsWith(PENDING_CHECKOUT_SAVE_KEY_PREFIX)) {
      doomed.push(name)
    }
  }
  doomed.forEach(name => window.sessionStorage.removeItem(name))
}

/**
 * Whether a choice is stored for an order, without reading or spending it.
 *
 * @param orderId - The order to look up.
 * @returns True when an entry exists, whoever owns it.
 */
export const hasCheckoutSave = (orderId: string | number): boolean =>
  hasStorage() && window.sessionStorage.getItem(keyFor(orderId)) !== null

/**
 * Reads the choice for an order and removes it in the same step, so it can be acted on once.
 *
 * @param orderId - The order that was just confirmed as paid.
 * @param owner - The currently signed-in account's email; an entry made by another account is dropped unused.
 * @returns The gateway module to store the method at, or null when there is no choice, it expired, it belongs to
 * another account or it is unreadable.
 */
export const consumeCheckoutSave = (orderId: string | number, owner: string | null | undefined): string | null => {
  if (!hasStorage()) {
    return null
  }
  const raw = window.sessionStorage.getItem(keyFor(orderId))
  if (raw === null) {
    return null
  }
  // Removed before anything is parsed or started: whatever happens next, the choice is spent.
  window.sessionStorage.removeItem(keyFor(orderId))
  try {
    const parsed = JSON.parse(raw) as Partial<PendingCheckoutSave>
    const key = ownerKey(owner)
    if (
      key !== null &&
      parsed.owner === key &&
      typeof parsed.gatewayModuleId === 'string' &&
      parsed.gatewayModuleId !== '' &&
      typeof parsed.expiresAt === 'number' &&
      parsed.expiresAt > Date.now()
    ) {
      return parsed.gatewayModuleId
    }
  } catch {
    // A damaged entry is the same as none.
  }
  return null
}
