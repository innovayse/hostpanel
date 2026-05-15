/**
 * GET /api/portal/client/domains/:id
 * Returns full information for a single client domain from the C# backend.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Domain ID is required' })

  return await internalApiCall<Record<string, unknown>>(event, `/me/domains/${id}`)
})
