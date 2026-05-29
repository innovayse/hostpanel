/**
 * POST /api/portal/order/:id/create-payment-intent
 * Creates a Stripe PaymentIntent via the C# backend.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Order ID is required' })

  return await internalApiCall<{ clientSecret: string }>(event, `/orders/${id}/create-payment-intent`, {
    method: 'POST',
  })
})
