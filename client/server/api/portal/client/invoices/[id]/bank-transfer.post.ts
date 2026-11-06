/**
 * POST /api/portal/client/invoices/:id/bank-transfer
 * Chooses manual bank transfer for the signed-in client's invoice via the C# backend and returns the bank details and the
 * reference to write on the transfer. The invoice id is the only input; the client comes from the session.
 * Requires an authenticated client session.
 *
 * @module server/api/portal/client/invoices/[id]/bank-transfer.post
 */

import { HttpMethod } from '~/types/httpmethod'

export default defineEventHandler(async (event) => {
  const id = requireRouteParam(event, 'id')

  return await internalApiCall<Record<string, unknown>>(event, `/me/invoices/${id}/bank-transfer`, {
    method: HttpMethod.Post,
  })
})
