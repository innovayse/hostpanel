/**
 * GET /api/portal/client/emails/:id
 * Returns the full message body of a single email from the C# backend.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Email ID is required' })

  return await internalApiCall<Record<string, unknown>>(event, `/me/emails/${id}`)
})
