/**
 * GET /api/portal/auth/2fa-status
 *
 * Returns the current user's 2FA enabled status.
 */
import { internalApiCall } from '~/server/utils/api'

export default defineEventHandler(async (event) => {
  return internalApiCall<{ enabled: boolean }>(event, '/auth/2fa/status')
})
