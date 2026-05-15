/**
 * GET /api/portal/client/domains/:id/nameservers
 * Returns the nameservers currently set on a domain via the C# backend.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Domain ID is required' })

  return await internalApiCall<Record<string, unknown>>(event, `/me/domains/${id}/nameservers`)
})
