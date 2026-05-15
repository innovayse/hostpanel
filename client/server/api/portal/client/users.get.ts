/**
 * GET /api/portal/client/users
 * Returns all users with access to the authenticated client account via the C# backend.
 */
export default defineEventHandler(async (event) => {
  return await internalApiCall<unknown[]>(event, '/me/users')
})
