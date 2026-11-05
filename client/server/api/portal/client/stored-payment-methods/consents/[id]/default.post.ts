/**
 * POST /api/portal/client/stored-payment-methods/consents/:id/default
 * Makes a stored payment method the default via the C# backend.
 * Requires an authenticated client session.
 *
 * @module server/api/portal/client/stored-payment-methods/consents/[id]/default.post
 */

import { HttpMethod } from '~/types/httpmethod'

export default defineEventHandler(async (event) => {
  const id = requireRouteParam(event, 'id')

  return await internalApiCall<Record<string, unknown>>(event, `/me/stored-payment-methods/consents/${id}/default`, {
    method: HttpMethod.Post,
  })
})
