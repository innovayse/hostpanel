/**
 * GET /api/portal/client/addresses
 * Returns the client's billing addresses from the C# backend.
 */
export default defineEventHandler(async (event) => {
  return await internalApiCall<unknown[]>(event, '/clients/me/addresses')
})
