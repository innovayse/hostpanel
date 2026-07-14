/**
 * GET /api/portal/client/email/:id/mailboxes
 * Returns all mailboxes for a business email domain from the C# backend.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Email domain ID is required' })

  return await internalApiCall<unknown[]>(event, `/my-email/${id}/mailboxes`)
})
