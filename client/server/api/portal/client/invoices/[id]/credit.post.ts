/**
 * POST /api/portal/client/invoices/:id/credit
 * Pays part or all of the signed-in client's invoice from their credit balance via the C# backend. Body: `{ amount: number }`.
 * The invoice id is the route's; the client comes from the session.
 * Requires an authenticated client session.
 *
 * @module server/api/portal/client/invoices/[id]/credit.post
 */

import { HttpMethod } from '~/types/httpmethod'

export default defineEventHandler(async (event) => {
  const id = requireRouteParam(event, 'id')

  const body = await readBody(event)
  return await internalApiCall<undefined>(event, `/me/invoices/${id}/credit`, {
    method: HttpMethod.Post,
    body,
  })
})
