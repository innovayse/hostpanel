/**
 * POST /api/portal/client/services/:id/plan-change
 * Requests a move of a hosting service to another plan via the C# backend.
 */

import { HttpMethod } from '~/types/httpmethod'

export default defineEventHandler(async (event) => {
  const id = requireRouteParam(event, 'id')

  const body = await readBody(event)
  return await internalApiCall<Record<string, unknown>>(event, `/me/services/${id}/plan-change`, {
    method: HttpMethod.Post,
    body,
  })
})
