/**
 * POST /api/portal/auth/2fa-login
 *
 * Completes the 2FA login flow: verifies the TOTP code against
 * the pending token, then sets auth cookies and returns user profile.
 */
import { setAuthCookie, setRefreshCookie } from '~/server/utils/api'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  const { pendingToken, code } = body ?? {}

  if (!pendingToken || !code) {
    throw createError({ statusCode: 400, statusMessage: 'pendingToken and code are required' })
  }

  const apiUrl = (useRuntimeConfig().apiUrl as string) || 'http://localhost:5000'

  let accessToken: string

  try {
    const response = await $fetch.raw<{ accessToken: string }>(
      `${apiUrl}/api/auth/2fa/login`,
      {
        method: 'POST',
        body: { pendingToken, code },
      }
    )

    accessToken = response._data?.accessToken ?? ''
    if (!accessToken) {
      throw createError({ statusCode: 502, statusMessage: 'Backend returned no access token' })
    }

    setAuthCookie(event, accessToken)

    const setCookieHeaders = response.headers.getSetCookie()
    for (const cookie of setCookieHeaders) {
      if (cookie.startsWith('refreshToken=')) {
        const value = cookie.split(';')[0]?.substring('refreshToken='.length)
        if (value) setRefreshCookie(event, value)
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
      statusMessage: data?.error ?? data?.message ?? 'Invalid authentication code',
    })
  }

  const user = await $fetch(`${apiUrl}/api/clients/me`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  })

  return user
})
