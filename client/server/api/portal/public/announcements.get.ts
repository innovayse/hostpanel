/**
 * GET /api/portal/public/announcements
 * Returns recent announcements from the C# backend.
 */
export default defineEventHandler(async (event) => {
  return await internalApiCall<unknown[]>(event, '/announcements?limitnum=10')
})
