/**
 * GET /api/portal/client/domains
 * Returns all domains for the authenticated client from the C# backend.
 */
export default defineEventHandler(async (event) => {
  return await internalApiCall<unknown[]>(event, '/me/domains')
})
