/**
 * GET /api/portal/client/announcements
 * Returns announcements from the C# backend.
 */
export default defineEventHandler(async (event) => {
  const query = getQuery(event)
  return await internalApiCall<Record<string, unknown>>(
    event,
    `/announcements?limitstart=${query.limitstart ?? 0}&limitnum=${query.limitnum ?? 50}`
  )
})
