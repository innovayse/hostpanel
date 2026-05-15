/**
 * GET /api/portal/public/departments
 * Returns active support departments from the C# backend.
 */
export default defineEventHandler(async (event) => {
  try {
    return await internalApiCall<unknown[]>(event, '/support/departments')
  } catch {
    return []
  }
})
