/**
 * POST /api/portal/auth/2fa-disable
 *
 * Disables 2FA for the current user.
 */
import { internalApiCall } from '~/server/utils/api'

export default defineEventHandler(async (event) => {
  return internalApiCall(event, '/auth/2fa/disable', { method: 'POST' })
})
