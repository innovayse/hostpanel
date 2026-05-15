/**
 * PUT /api/portal/client/payment-methods/:id
 * Updates a stored payment method via the C# backend.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Payment method ID is required' })

  const body = await readBody(event)
  return await internalApiCall<Record<string, unknown>>(event, `/me/payment-methods/${id}`, {
    method: 'PUT',
    body,
  })
})
