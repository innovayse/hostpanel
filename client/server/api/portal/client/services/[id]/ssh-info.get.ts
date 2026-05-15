/**
 * GET /api/portal/client/services/:id/ssh-info
 * Returns SSH access details for a hosting service via the C# backend.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Service ID is required' })

  return await internalApiCall<Record<string, unknown>>(event, `/me/services/${id}/ssh-info`)
})
