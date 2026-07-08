/**
 * GET /api/portal/client/email/:id
 * Returns full details for a single business email domain from the C# backend.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Email domain ID is required' })

  return await internalApiCall<Record<string, unknown>>(event, `/my-email/${id}`)
})
