/**
 * POST /api/portal/order/:id/confirm-payment
 * Confirms Stripe payment and auto-accepts the order.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Order ID is required' })

  const body = await readBody(event)
  return await internalApiCall<{ success: boolean }>(event, `/orders/${id}/confirm-payment`, {
    method: 'POST',
    body,
  })
})
