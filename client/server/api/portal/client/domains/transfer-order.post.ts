/**
 * POST /api/portal/client/domains/transfer-order
 * Creates a domain transfer order via the C# backend.
 *
 * Body: { domain: string, eppcode?: string, years?: number, paymentmethod: string }
 */
export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  return await internalApiCall<Record<string, unknown>>(event, '/me/domains/transfer-order', {
    method: 'POST',
    body,
  })
})
