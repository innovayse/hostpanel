/**
 * DELETE /api/portal/client/email/:id/aliases/:aliasId
 * Deletes an alias from a business email domain via the C# backend.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Email domain ID is required' })

  const aliasId = getRouterParam(event, 'aliasId')
  if (!aliasId) throw createError({ statusCode: 400, statusMessage: 'Alias ID is required' })

  return await internalApiCall<Record<string, unknown>>(event, `/my-email/${id}/aliases/${aliasId}`, {
    method: 'DELETE',
  })
})
