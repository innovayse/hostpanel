/**
 * POST /api/portal/client/email/:id/aliases
 * Creates a new alias for a business email domain via the C# backend.
 *
 * Body: { localPart: string, targetAddress: string }
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Email domain ID is required' })

  const body = await readBody(event)
  return await internalApiCall<Record<string, unknown>>(event, `/my-email/${id}/aliases`, {
    method: 'POST',
    body,
  })
})
