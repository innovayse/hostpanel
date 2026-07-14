/**
 * POST /api/portal/client/email/:id/mailboxes
 * Creates a new mailbox for a business email domain via the C# backend.
 *
 * Body: { localPart: string, password: string, displayName?: string }
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Email domain ID is required' })

  const body = await readBody(event)
  return await internalApiCall<Record<string, unknown>>(event, `/my-email/${id}/mailboxes`, {
    method: 'POST',
    body,
  })
})
