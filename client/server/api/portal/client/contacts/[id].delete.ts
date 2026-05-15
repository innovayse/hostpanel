/**
 * DELETE /api/portal/client/contacts/:id
 * Removes a contact from the client's account via the C# backend.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Contact ID is required.' })

  return await internalApiCall<Record<string, unknown>>(event, `/clients/me/contacts/${id}`, {
    method: 'DELETE',
  })
})
