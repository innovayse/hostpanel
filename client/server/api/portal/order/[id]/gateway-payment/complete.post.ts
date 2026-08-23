/**
 * POST /api/portal/order/:id/gateway-payment/complete
 * Verifies a hosted-gateway payment for an order against the C# backend,
 * which pulls the authoritative status from the bank.
 * Anonymous — matches the start endpoint's auth model.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Order ID is required' })

  return await internalApiCall<{ state: 'paid' | 'pending' | 'declined' }>(
    event, `/orders/${id}/gateway-payment/complete`, { method: 'POST' })
})
