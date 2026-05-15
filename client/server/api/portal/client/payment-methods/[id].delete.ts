/**
 * DELETE /api/portal/client/payment-methods/:id
 * Removes a stored payment method via the C# backend.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Payment method ID is required' })

  return await internalApiCall<Record<string, unknown>>(event, `/me/payment-methods/${id}`, {
    method: 'DELETE',
  })
})
