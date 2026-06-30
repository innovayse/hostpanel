/**
 * POST /api/portal/auth/2fa-setup
 *
 * Generates a new TOTP secret and returns the QR code URI for scanning.
 */
import { internalApiCall } from '~/server/utils/api'

export default defineEventHandler(async (event) => {
  return internalApiCall<{ secret: string; qrCodeUri: string }>(event, '/auth/2fa/setup', {
    method: 'POST',
  })
})
