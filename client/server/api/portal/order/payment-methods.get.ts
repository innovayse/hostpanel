/**
 * GET /api/portal/order/payment-methods
 * Returns active payment gateways from the C# backend.
 * No authentication required.
 */
export default defineEventHandler(async (event) => {
  return await internalApiCall<unknown[]>(event, '/payment-methods')
})
