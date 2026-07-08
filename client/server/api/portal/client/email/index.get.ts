/**
 * GET /api/portal/client/email
 * Returns all business email domains for the authenticated client from the C# backend.
 */
export default defineEventHandler(async (event) => {
  return await internalApiCall<unknown[]>(event, '/my-email')
})
