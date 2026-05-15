/**
 * POST /api/portal/auth/login
 * Authenticates a client against the C# backend and sets a JWT auth cookie.
 */
export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  const { email, password } = body ?? {}

  if (!email || !password) {
    throw createError({ statusCode: 400, statusMessage: 'Email and password are required' })
  }

  const response = await internalApiCall<{ accessToken: string; expiresAt: string; role: string }>(
    event,
    '/auth/login',
    { method: 'POST', body: { email, password } }
  )

  setAuthCookie(event, response.accessToken)

  // Fetch full user profile with the new token
  const user = await $fetch(`${(useRuntimeConfig().apiUrl as string) || 'http://localhost:5000'}/api/clients/me`, {
    headers: { Authorization: `Bearer ${response.accessToken}` }
  })

  return user
})
