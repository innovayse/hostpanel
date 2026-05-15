/**
 * GET /api/portal/client/emails
 * Returns email history for the authenticated client from the C# backend.
 */
export default defineEventHandler(async (event) => {
  return await internalApiCall<unknown[]>(event, '/me/emails')
})
