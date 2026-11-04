/**
 * POST /api/portal/client/payment-methods/sessions/complete
 * Finishes saving a payment method once the payer is back from the gateway, or the embedded form
 * confirmed it. The backend reads the result back from the gateway before it counts the method as
 * saved, and the call is idempotent.
 *
 * Body: { gatewayModuleId: string, sessionId: string }.
 *
 * @module server/api/portal/client/payment-methods/sessions/complete.post
 */

import { HttpMethod } from '~/types/httpmethod'
import { readBodyStrings } from '~/server/utils/readBodyObject'

export default defineEventHandler(async (event) => {
  const { gatewayModuleId, sessionId } = await readBodyStrings(event, 'gatewayModuleId', 'sessionId')

  return await internalApiCall<unknown>(event, '/me/payment-methods/sessions/complete', {
    method: HttpMethod.Post,
    body: { gatewayModuleId, sessionId },
  })
})
