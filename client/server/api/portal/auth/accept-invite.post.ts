/**
 * POST /api/portal/auth/accept-invite
 *
 * Accepts a user invitation by setting the password, then returns an
 * authenticated session. Captures both access and refresh tokens from
 * the backend response.
 */
import { setAuthCookie, setRefreshCookie } from '~/server/utils/api'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  const { token, password } = body ?? {}

  if (!token || !password) {
    throw createError({ statusCode: 400, statusMessage: 'Token and password are required' })
  }

  const config = useRuntimeConfig()
  const apiUrl = (config.apiUrl as string) || 'http://localhost:5000'

  let accessToken: string

  try {
    const response = await $fetch.raw<{ accessToken: string; expiresAt: string; role: string }>(
      `${apiUrl}/api/auth/accept-invite`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token, password }),
      }
    )

    accessToken = response._data?.accessToken ?? ''
    if (!accessToken) {
      throw createError({ statusCode: 502, statusMessage: 'Backend returned no access token' })
    }

    setAuthCookie(event, accessToken)

    // Extract refresh token from backend's Set-Cookie header
    const setCookieHeaders = response.headers.getSetCookie()
    for (const cookie of setCookieHeaders) {
      if (cookie.startsWith('refreshToken=')) {
        const value = cookie.split(';')[0]?.substring('refreshToken='.length)
        if (value) {
          setRefreshCookie(event, value)
        }
        break
      }
    }
  } catch (err: unknown) {
    if ((err as { statusCode?: number })?.statusCode === 400 || (err as { statusCode?: number })?.statusCode === 502) {
      throw err
    }
    const fetchErr = err as { status?: number; statusCode?: number; data?: unknown; message?: string }
    const status = fetchErr?.status ?? fetchErr?.statusCode ?? 400
    const data = fetchErr?.data as { error?: string; message?: string } | undefined
    throw createError({
      statusCode: status,
      statusMessage: data?.error ?? data?.message ?? 'Failed to accept invitation',
    })
  }

  // Fetch full user profile with the new token
  try {
    const user = await $fetch(`${apiUrl}/api/clients/me`, {
      headers: { Authorization: `Bearer ${accessToken}` },
    })
    return user
  } catch {
    return { success: true }
  }
})
