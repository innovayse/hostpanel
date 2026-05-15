/**
 * GET /api/portal/client/tickets
 * Returns all support tickets for the authenticated client from the C# backend.
 */
export default defineEventHandler(async (event) => {
  return await internalApiCall<unknown[]>(event, '/me/tickets')
})
