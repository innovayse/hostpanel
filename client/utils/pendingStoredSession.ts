/**
 * Remembers an open save session across the trip to the gateway's page.
 *
 * The session id is only known after the return address was sent, so it cannot ride in the
 * address; session storage keeps it for this tab until the payer comes back.
 *
 * @module utils/pendingStoredSession
 */

import { PENDING_STORED_SESSION_KEY } from '~/constants/storedMethodReturn'
import type { PendingStoredSession } from '~/types/pendingstoredsession'

/**
 * Stores the pending session.
 *
 * @param pending - The session to remember.
 */
export const writePendingStoredSession = (pending: PendingStoredSession): void => {
  window.sessionStorage.setItem(PENDING_STORED_SESSION_KEY, JSON.stringify(pending))
}

/**
 * Reads the pending session.
 *
 * @returns The remembered session, or null when there is none or it is unreadable.
 */
export const readPendingStoredSession = (): PendingStoredSession | null => {
  const raw = window.sessionStorage.getItem(PENDING_STORED_SESSION_KEY)
  if (raw === null) {
    return null
  }
  try {
    const parsed = JSON.parse(raw) as Partial<PendingStoredSession>
    if (typeof parsed.gatewayModuleId === 'string' && typeof parsed.sessionId === 'string') {
      return { gatewayModuleId: parsed.gatewayModuleId, sessionId: parsed.sessionId }
    }
  } catch {
    // A damaged entry is the same as none.
  }
  return null
}

/** Forgets the pending session. */
export const clearPendingStoredSession = (): void => {
  window.sessionStorage.removeItem(PENDING_STORED_SESSION_KEY)
}
