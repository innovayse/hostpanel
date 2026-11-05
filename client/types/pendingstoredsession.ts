/**
 * A save session the payer left the portal for.
 *
 * @module types/pendingstoredsession
 */

/** What the portal remembers across the redirect so it can complete the session on return. */
export interface PendingStoredSession {
  /** Gateway module the session was opened at. */
  gatewayModuleId: string
  /** The gateway's session id. */
  sessionId: string
}
