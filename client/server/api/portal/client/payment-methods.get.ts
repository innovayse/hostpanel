/**
 * GET /api/portal/client/payment-methods
 * Returns stored payment methods for the authenticated client from the C# backend.
 */
export default defineEventHandler(async (event) => {
  return await internalApiCall<unknown[]>(event, '/me/payment-methods')
})
