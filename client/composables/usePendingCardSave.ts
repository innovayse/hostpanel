/**
 * Remembers the save session a payer left the portal with for a gateway's hosted page, so the
 * tab they come back to can complete it.
 *
 * The session id is only known after the portal asked the API to start it, which is after the
 * address the gateway returns the payer to was fixed; the payer therefore comes back with the
 * module in the address and nothing else. The session lives in `sessionStorage`: the payer leaves
 * and returns in the same tab, and a shared computer keeps nothing once the tab is closed. An entry
 * also expires after {@link PENDING_CARD_SAVE_TTL_MS}.
 *
 * @module composables/usePendingCardSave
 */

/** Prefix for the per-module storage keys. */
const STORAGE_PREFIX = 'pending-card-save:'

/** How long a remembered session stays usable: longer than a gateway page, short enough not to linger. */
export const PENDING_CARD_SAVE_TTL_MS = 60 * 60 * 1000

/** What is kept for one module while the payer is at the gateway. */
interface StoredCardSave {
  /** The save session the API opened. */
  sessionId: string
  /** Epoch milliseconds after which the entry is no longer used. */
  expiresAt: number
}

/**
 * True when a parsed storage value has the {@link StoredCardSave} shape.
 *
 * @param value - The parsed storage value.
 * @returns True when the value can be used as an entry.
 */
const isStored = (value: unknown): value is StoredCardSave => {
  if (typeof value !== 'object' || value === null) {
    return false
  }
  const entry = value as Record<string, unknown>
  return typeof entry.sessionId === 'string' && typeof entry.expiresAt === 'number'
}

/**
 * Reads, writes and clears the pending save session of a gateway module.
 *
 * Every function is a no-op on the server, where storage does not exist.
 *
 * @returns The remember/recall/forget functions.
 */
export const usePendingCardSave = () => {
  /**
   * Stores the session the payer is leaving with.
   *
   * @param gatewayModuleId - The gateway module that holds the session.
   * @param sessionId - The session the API opened.
   */
  const remember = (gatewayModuleId: string, sessionId: string): void => {
    if (!import.meta.client) {
      return
    }
    const entry: StoredCardSave = { sessionId, expiresAt: Date.now() + PENDING_CARD_SAVE_TTL_MS }
    sessionStorage.setItem(`${STORAGE_PREFIX}${gatewayModuleId}`, JSON.stringify(entry))
  }

  /**
   * Reads the pending session of a module, dropping it when it has expired or is malformed.
   *
   * @param gatewayModuleId - The gateway module the payer came back from.
   * @returns The session id, or `undefined` when none is stored or it is no longer valid.
   */
  const recall = (gatewayModuleId: string): string | undefined => {
    if (!import.meta.client) {
      return undefined
    }
    const key = `${STORAGE_PREFIX}${gatewayModuleId}`
    const raw = sessionStorage.getItem(key)
    if (raw === null) {
      return undefined
    }
    try {
      const parsed: unknown = JSON.parse(raw)
      if (isStored(parsed) && parsed.expiresAt > Date.now()) {
        return parsed.sessionId
      }
    } catch {
      // A malformed entry is dropped below like an expired one.
    }
    sessionStorage.removeItem(key)
    return undefined
  }

  /**
   * Drops the pending session of a module once it has been completed or refused.
   *
   * @param gatewayModuleId - The gateway module whose session is no longer needed.
   */
  const forget = (gatewayModuleId: string): void => {
    if (!import.meta.client) {
      return
    }
    sessionStorage.removeItem(`${STORAGE_PREFIX}${gatewayModuleId}`)
  }

  return { remember, recall, forget }
}
