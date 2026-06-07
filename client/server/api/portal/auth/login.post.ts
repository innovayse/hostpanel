/**
 * POST /api/portal/auth/login
 *
 * Authenticates a client against the C# backend.
 * Captures the access token from the response body and the refresh token
 * from the Set-Cookie header, then sets both as httpOnly cookies.
 */
import { setAuthCookie, setRefreshCookie } from '~/server/utils/api'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  const { email, password } = body ?? {}

  if (!email || !password) {
    throw createError({ statusCode: 400, statusMessage: 'Email and password are required' })
  }

  const config = useRuntimeConfig()
  const apiUrl = (config.apiUrl as string) || 'http://localhost:5000'

  let accessToken: string

  try {
    const response = await $fetch.raw<{ accessToken: string; expiresAt: string; role: string }>(
      `${apiUrl}/api/auth/login`,
      {
        method: 'POST',
        body: { email, password },
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
    const status = fetchErr?.status ?? fetchErr?.statusCode ?? 401
    const data = fetchErr?.data as { error?: string; message?: string } | undefined
    throw createError({
      statusCode: status,
      statusMessage: data?.error ?? data?.message ?? 'Login failed',
    })
  }

  // Fetch full user profile with the new token
  const user = await $fetch(`${(useRuntimeConfig().apiUrl as string) || 'http://localhost:5000'}/api/clients/me`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  })

  return user
})
