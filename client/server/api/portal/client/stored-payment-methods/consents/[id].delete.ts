/**
 * DELETE /api/portal/client/stored-payment-methods/consents/:id
 * Revokes a stored charge consent via the C# backend.
 * Requires an authenticated client session.
 *
 * @module server/api/portal/client/stored-payment-methods/consents/[id].delete
 */

import { HttpMethod } from '~/types/httpmethod'

export default defineEventHandler(async (event) => {
  const id = requireRouteParam(event, 'id')

  return await internalApiCall<Record<string, unknown>>(event, `/me/stored-payment-methods/consents/${id}`, {
    method: HttpMethod.Delete,
  })
})
