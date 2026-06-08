/**
 * POST /api/portal/auth/2fa-enable
 *
 * Verifies the TOTP code and enables 2FA for the current user.
 */
import { internalApiCall } from '~/server/utils/api'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  return internalApiCall(event, '/auth/2fa/enable', { method: 'POST', body })
})
