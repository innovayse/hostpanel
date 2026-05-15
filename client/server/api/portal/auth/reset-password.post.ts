/**
 * POST /api/portal/auth/reset-password
 * Resets a user's password using a token received via email.
 */
export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  const { email, token, newPassword } = body ?? {}

  if (!email || !token || !newPassword) {
    throw createError({ statusCode: 400, statusMessage: 'Email, token, and new password are required.' })
  }

  return await internalApiCall(event, '/auth/reset-password', {
    method: 'POST',
    body: { email, token, newPassword },
  })
})
