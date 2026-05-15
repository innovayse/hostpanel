/**
 * POST /api/portal/client/addresses
 * Adds a new billing address via the C# backend.
 */
export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  return await internalApiCall<Record<string, unknown>>(event, '/clients/me/addresses', {
    method: 'POST',
    body,
  })
})
