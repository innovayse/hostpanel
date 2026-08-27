/**
 * POST /api/portal/auth/2fa-enable
 * Switches two-factor authentication on for the authenticated account.
 *
 * Body: { code: string } — the digits the authenticator app currently shows.
 */
export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  return await internalApiCall<void>(event, '/me/2fa/enable', { method: 'POST', body })
})
