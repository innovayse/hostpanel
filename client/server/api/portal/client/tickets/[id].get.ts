/**
 * GET /api/portal/client/tickets/:id
 * Returns the full thread of a single support ticket from the C# backend.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Ticket ID is required' })

  return await internalApiCall<Record<string, unknown>>(event, `/me/tickets/${id}`)
})
