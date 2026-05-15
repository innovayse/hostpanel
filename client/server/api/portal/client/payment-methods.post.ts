/**
 * POST /api/portal/client/payment-methods
 * Adds a new stored payment method for the authenticated client via the C# backend.
 */
export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  return await internalApiCall<Record<string, unknown>>(event, '/me/payment-methods', {
    method: 'POST',
    body,
  })
})
