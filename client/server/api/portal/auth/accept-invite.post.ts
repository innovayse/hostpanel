/**
 * POST /api/portal/auth/accept-invite
 * Accepts a user invitation by setting the password, then returns an authenticated session.
 * Sets the JWT auth cookie so the user is logged in immediately after accepting.
 */
export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  const { token, password } = body ?? {}

  if (!token || !password) {
    throw createError({ statusCode: 400, statusMessage: 'Token and password are required' })
  }

  const response = await internalApiCall<{ accessToken: string; expiresAt: string; role: string }>(
    event,
    '/auth/accept-invite',
    { method: 'POST', body: { token, password } }
  )

  setAuthCookie(event, response.accessToken)

  // Fetch full user profile with the new token
  const config = useRuntimeConfig()
  const apiUrl = (config.apiUrl as string) || 'http://localhost:5000'
  try {
    const user = await $fetch(`${apiUrl}/api/clients/me`, {
      headers: { Authorization: `Bearer ${response.accessToken}` }
    })
    return user
  } catch {
    return { success: true }
  }
})
