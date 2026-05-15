/**
 * PUT /api/portal/client/contacts/:id
 * Updates an existing contact via the C# backend.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Contact ID is required.' })

  const body = await readBody(event)
  return await internalApiCall<Record<string, unknown>>(event, `/clients/me/contacts/${id}`, {
    method: 'PUT',
    body,
  })
})
