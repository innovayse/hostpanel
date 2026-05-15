/**
 * DELETE /api/portal/client/users/:id
 * Removes a user's access to the authenticated client account via the C# backend.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'User ID is required.' })

  return await internalApiCall<Record<string, unknown>>(event, `/me/users/${id}`, {
    method: 'DELETE',
  })
})
