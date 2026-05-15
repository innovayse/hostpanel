/**
 * GET /api/portal/client/services/:id/cancellation-status
 * Checks if there is a pending cancellation request for this service via the C# backend.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Service ID is required' })

  return await internalApiCall<Record<string, unknown>>(event, `/me/services/${id}/cancellation-status`)
})
