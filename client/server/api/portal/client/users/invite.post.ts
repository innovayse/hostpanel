/**
 * POST /api/portal/client/users/invite
 * Invites a new user to the authenticated client account via the C# backend.
 *
 * Body: { email: string, permissions: 'all' | 'choose' }
 */
export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  return await internalApiCall<Record<string, unknown>>(event, '/me/users/invite', {
    method: 'POST',
    body,
  })
})
