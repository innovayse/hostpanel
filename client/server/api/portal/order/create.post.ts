/**
 * POST /api/portal/order/create
 * Creates an order via the C# backend.
 * Supports both authenticated clients and guest checkout.
 */
export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  return await internalApiCall<Record<string, unknown>>(event, '/orders', {
    method: 'POST',
    body,
  })
})
