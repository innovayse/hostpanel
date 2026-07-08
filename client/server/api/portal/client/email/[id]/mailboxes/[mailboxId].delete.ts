/**
 * DELETE /api/portal/client/email/:id/mailboxes/:mailboxId
 * Deletes a mailbox from a business email domain via the C# backend.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Email domain ID is required' })

  const mailboxId = getRouterParam(event, 'mailboxId')
  if (!mailboxId) throw createError({ statusCode: 400, statusMessage: 'Mailbox ID is required' })

  return await internalApiCall<Record<string, unknown>>(event, `/my-email/${id}/mailboxes/${mailboxId}`, {
    method: 'DELETE',
  })
})
