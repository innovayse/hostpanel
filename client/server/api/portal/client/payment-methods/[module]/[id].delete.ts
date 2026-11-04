/**
 * DELETE /api/portal/client/payment-methods/:module/:id
 * Removes a stored payment method of a gateway module via the C# backend.
 *
 * @module server/api/portal/client/payment-methods/[module]/[id].delete
 */

import { HttpMethod } from '~/types/httpmethod'
import { RouteParamKind } from '~/types/routeparamkind'

export default defineEventHandler(async (event) => {
  const module = requireRouteParam(event, 'module', RouteParamKind.Identifier)
  const id = requireRouteParam(event, 'id', RouteParamKind.Identifier)

  return await internalApiCall<unknown>(event, `/me/payment-methods/${module}/${id}`, {
    method: HttpMethod.Delete,
  })
})
