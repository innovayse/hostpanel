/**
 * POST /api/portal/client/services/:id/setup
 * Finalizes service setup (domain, username, password) via the C# backend.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Service ID is required' })

  const body = await readBody(event)
  return await internalApiCall<Record<string, unknown>>(event, `/me/services/${id}/setup`, {
    method: 'POST',
    body,
  })
})
