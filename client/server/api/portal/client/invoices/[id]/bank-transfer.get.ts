/**
 * GET /api/portal/client/invoices/:id/bank-transfer
 * Returns the bank details and the reference of the manual bank transfer waiting on the signed-in client's invoice, from the
 * C# backend. Answers a refusal when no transfer waits on it.
 * Requires an authenticated client session.
 *
 * @module server/api/portal/client/invoices/[id]/bank-transfer.get
 */

export default defineEventHandler(async (event) => {
  const id = requireRouteParam(event, 'id')

  return await internalApiCall<Record<string, unknown>>(event, `/me/invoices/${id}/bank-transfer`)
})
