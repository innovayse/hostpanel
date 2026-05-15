/**
 * POST /api/portal/client/contacts
 * Adds a new sub-contact to the client's account via the C# backend.
 */
export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  return await internalApiCall<Record<string, unknown>>(event, '/clients/me/contacts', {
    method: 'POST',
    body,
  })
})
