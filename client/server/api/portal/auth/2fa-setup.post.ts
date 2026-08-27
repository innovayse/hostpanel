/**
 * POST /api/portal/auth/2fa-setup
 * Issues a TOTP secret and the otpauth URI for the authenticated account.
 *
 * Enrolment only — two-factor stays off until `2fa-enable` is called with a code.
 */
export default defineEventHandler(async (event) => {
  return await internalApiCall<{ secret: string; qrCodeUri: string }>(event, '/me/2fa/setup', {
    method: 'POST',
  })
})
