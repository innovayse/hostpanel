/**
 * POST /api/portal/client/services/:id/cancel
 * Submits a cancellation request for a hosting service via the C# backend.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Service ID is required' })

  const body = await readBody(event)
  return await internalApiCall<Record<string, unknown>>(event, `/me/services/${id}/cancel`, {
    method: 'POST',
    body,
  })
})
