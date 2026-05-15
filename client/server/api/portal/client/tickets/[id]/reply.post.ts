/**
 * POST /api/portal/client/tickets/:id/reply
 * Adds a client reply to an existing support ticket via the C# backend.
 *
 * Body: { message }
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Ticket ID is required' })

  const body = await readBody(event)
  return await internalApiCall<Record<string, unknown>>(event, `/me/tickets/${id}/reply`, {
    method: 'POST',
    body,
  })
})
