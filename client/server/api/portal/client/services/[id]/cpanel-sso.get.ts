/**
 * GET /api/portal/client/services/:id/cpanel-sso
 * Generates a one-time SSO login URL via the C# backend.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Service ID is required' })

  return await internalApiCall<{ url: string }>(event, `/me/services/${id}/cpanel-sso`)
})
