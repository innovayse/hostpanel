/**
 * POST /api/portal/client/tickets
 * Opens a new support ticket via the C# backend.
 *
 * Body: { deptid, subject, message, priority? }
 */
export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  return await internalApiCall<Record<string, unknown>>(event, '/me/tickets', {
    method: 'POST',
    body,
  })
})
