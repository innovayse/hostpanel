/**
 * GET /api/portal/public/departments
 * Returns active support departments from the C# backend.
 */
export default defineEventHandler(async (event) => {
  // No try/catch swallowing the failure: '/support/departments' was a path the API
  // never exposed, so every call 404'd into an empty array and the ticket form's
  // department picker rendered empty with nothing in the logs to say why.
  return await internalApiCall<unknown[]>(event, '/departments')
})
