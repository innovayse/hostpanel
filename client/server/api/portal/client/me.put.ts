/**
 * PUT /api/portal/client/me
 * Updates the authenticated client's profile via the C# backend.
 */
export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  return await internalApiCall<Record<string, unknown>>(event, '/clients/me', {
    method: 'PUT',
    body,
  })
})
