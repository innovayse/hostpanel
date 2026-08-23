/**
 * POST /api/portal/client/invoices/:id/gateway-payment/complete
 * Verifies a hosted-gateway payment for an invoice against the C# backend,
 * which pulls the authoritative status from the bank.
 * Requires an authenticated client session.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Invoice ID is required' })

  return await internalApiCall<{ state: 'paid' | 'pending' | 'declined' }>(
    event, `/me/invoices/${id}/gateway-payment/complete`, { method: 'POST' })
})
