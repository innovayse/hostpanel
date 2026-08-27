/**
 * POST /api/portal/auth/2fa-disable
 * Switches two-factor authentication off for the authenticated account.
 *
 * Body: { code: string }. A code is required rather than just a signed-in session — removing
 * the second factor is exactly what someone who had taken over a session would want to do.
 */
export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  return await internalApiCall<void>(event, '/me/2fa/disable', { method: 'POST', body })
})
