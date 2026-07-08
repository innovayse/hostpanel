/**
 * POST /api/portal/client/email/create
 * Creates a new business email domain for the authenticated client via the C# backend.
 *
 * Body: { domain: string }
 */
export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  return await internalApiCall<Record<string, unknown>>(event, '/my-email', {
    method: 'POST',
    body,
  })
})
