/**
 * POST /api/portal/order/:id/gateway-payment/start
 * Starts a hosted-gateway payment (e.g. Inecobank) for an order via the C# backend.
 * Body: { module: string, returnUrl: string }
 * Anonymous — the order/invoice already exist from checkout.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Order ID is required' })

  const body = await readBody(event)
  return await internalApiCall<{ redirectUrl: string }>(event, `/orders/${id}/gateway-payment/start`, {
    method: 'POST',
    body,
  })
})
