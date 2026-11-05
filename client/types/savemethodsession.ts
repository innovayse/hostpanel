/**
 * A session opened to store a payment method.
 *
 * @module types/savemethodsession
 */

import type { SaveMethodMode } from '~/types/savemethodmode'

/** What `POST /api/me/stored-payment-methods/sessions` answers with. */
export interface SaveMethodSession {
  /** The gateway's session id; completing the save names it. */
  sessionId: string
  /** How the payer enters the method. */
  mode: SaveMethodMode
  /** Token for an in-portal form; set in the embedded mode only. */
  clientToken: string | null
  /** Where to send the payer; set in the redirect mode only. */
  redirectUrl: string | null
}
