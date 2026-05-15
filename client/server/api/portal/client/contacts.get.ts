/**
 * GET /api/portal/client/contacts
 * Returns all sub-contacts for the authenticated client from the C# backend.
 */
export default defineEventHandler(async (event) => {
  return await internalApiCall<unknown[]>(event, '/clients/me/contacts')
})
