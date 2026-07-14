/**
 * POST /api/portal/client/email/:id/verify-dns
 * Triggers DNS verification for a business email domain via the C# backend.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Email domain ID is required' })

  return await internalApiCall<Record<string, unknown>>(event, `/my-email/${id}/verify-dns`, {
    method: 'POST',
  })
})
