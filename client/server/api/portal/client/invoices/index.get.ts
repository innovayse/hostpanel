/**
 * GET /api/portal/client/invoices
 * Returns the authenticated client's invoice list from the C# backend.
 */
export default defineEventHandler(async (event) => {
  return await internalApiCall<unknown[]>(event, '/me/invoices')
})
