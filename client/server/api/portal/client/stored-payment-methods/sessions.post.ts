/**
 * POST /api/portal/client/stored-payment-methods/sessions
 * Opens a save session for a payment method via the C# backend.
 * Body: { gatewayModuleId: string, consentAccepted: boolean, consentTextVersion: ConsentTextVersion, returnUrl: string }
 * (the backend refuses a session without a non-empty `consentTextVersion` of at most 50 characters).
 * Requires an authenticated client session.
 *
 * @module server/api/portal/client/stored-payment-methods/sessions.post
 */

import type { SaveMethodSession } from '~/types/savemethodsession'
import { HttpMethod } from '~/types/httpmethod'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  return await internalApiCall<SaveMethodSession>(event, '/me/stored-payment-methods/sessions', {
    method: HttpMethod.Post,
    body,
  })
})
