/**
 * POST /api/portal/client/invoices/:id/gateway-payment/start
 * Starts a hosted-gateway payment (e.g. Inecobank) for an invoice via the C# backend.
 * Body: { module: string, returnUrl: string }
 * Requires an authenticated client session.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Invoice ID is required' })

  const body = await readBody(event)
  return await internalApiCall<{ redirectUrl: string }>(event, `/me/invoices/${id}/gateway-payment/start`, {
    method: 'POST',
    body,
  })
})
