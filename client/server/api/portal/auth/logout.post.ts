/**
 * POST /api/portal/auth/logout
 *
 * Revokes the refresh token on the backend and clears all auth cookies.
 * Forwards the refresh token as a Cookie header since the backend reads
 * it from `Request.Cookies["refreshToken"]`.
 */
import { clearAllAuthCookies } from '~/server/utils/api'

export default defineEventHandler(async (event) => {
  const refreshToken = getCookie(event, 'refresh_token')

  try {
    const config = useRuntimeConfig()
    const apiUrl = (config.apiUrl as string) || 'http://localhost:5000'

    await $fetch(`${apiUrl}/api/auth/logout`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(refreshToken ? { Cookie: `refreshToken=${refreshToken}` } : {}),
      },
    })
  } catch {
    // Ignore backend errors on logout — clear cookies regardless
  }

  clearAllAuthCookies(event)
  return { ok: true }
})
