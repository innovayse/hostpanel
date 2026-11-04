/**
 * GET /api/portal/client/payment-methods/modules
 * Lists the gateway modules the signed-in client can save a payment method with.
 *
 * @module server/api/portal/client/payment-methods/modules.get
 */

export default defineEventHandler(async (event) => {
  return await internalApiCall<unknown>(event, '/me/payment-methods/modules')
})
