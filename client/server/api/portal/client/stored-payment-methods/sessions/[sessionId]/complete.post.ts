/**
 * POST /api/portal/client/stored-payment-methods/sessions/:sessionId/complete
 * Finishes a save session via the C# backend.
 * Body: { gatewayModuleId: string }
 * Requires an authenticated client session.
 *
 * @module server/api/portal/client/stored-payment-methods/sessions/[sessionId]/complete.post
 */

import type { StoredMethod } from '~/types/storedmethod'
import { HttpMethod } from '~/types/httpmethod'
import { RouteParamKind } from '~/types/routeparamkind'

export default defineEventHandler(async (event) => {
  const sessionId = requireRouteParam(event, 'sessionId', RouteParamKind.Identifier)

  const body = await readBody(event)
  return await internalApiCall<StoredMethod>(event, `/me/stored-payment-methods/sessions/${sessionId}/complete`, {
    method: HttpMethod.Post,
    body,
  })
})
