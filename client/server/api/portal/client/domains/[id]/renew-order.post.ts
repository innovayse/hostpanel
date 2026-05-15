/**
 * POST /api/portal/client/domains/:id/renew-order
 * Creates a domain renewal order via the C# backend.
 *
 * Body: { years: number, paymentmethod: string }
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Domain ID is required' })

  const body = await readBody(event)
  return await internalApiCall<Record<string, unknown>>(event, `/me/domains/${id}/renew-order`, {
    method: 'POST',
    body,
  })
})
