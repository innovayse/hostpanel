/**
 * A started save session, as the API answers `POST /me/payment-methods/sessions`.
 *
 * @module types/savedmethodsession
 */

import type { SaveMethodMode } from '~/types/savemethodmode'

/** One save session the browser has to finish. */
export interface SavedMethodSession {
  /** The gateway module that holds the session. */
  gatewayModuleId: string
  /** The session to hand back to the API once the payer is done. */
  sessionId: string
  /** How the browser finishes the session. */
  mode: SaveMethodMode
  /** For an embedded session, the token the card form confirms with; null otherwise. */
  clientToken: string | null
  /** For a redirect session, the hosted page to send the payer to; null otherwise. */
  redirectUrl: string | null
}
