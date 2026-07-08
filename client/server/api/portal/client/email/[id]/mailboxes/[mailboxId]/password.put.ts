/**
 * PUT /api/portal/client/email/:id/mailboxes/:mailboxId/password
 * Updates the password for a mailbox in a business email domain via the C# backend.
 *
 * Body: { password: string }
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Email domain ID is required' })

  const mailboxId = getRouterParam(event, 'mailboxId')
  if (!mailboxId) throw createError({ statusCode: 400, statusMessage: 'Mailbox ID is required' })

  const body = await readBody(event)
  return await internalApiCall<Record<string, unknown>>(event, `/my-email/${id}/mailboxes/${mailboxId}/password`, {
    method: 'PUT',
    body,
  })
})
