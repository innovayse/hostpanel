/**
 * C# backend API utility -- server-side only
 *
 * All backend API calls go through `internalApiCall`. It:
 * - Reads `apiUrl` from runtimeConfig (set via API_URL env var)
 * - Attaches the JWT access token from the `auth_token` httpOnly cookie
 * - On 401: silently refreshes the token using the `refresh_token` cookie
 *   and retries the request once
 * - Throws appropriate H3 errors on failure
 */

import type { H3Event } from 'h3'

/** Module-level lock to prevent concurrent refresh calls. */
let refreshPromise: Promise<string | null> | null = null

/**
 * Resolve the C# backend base URL from runtime config.
 *
 * @returns The backend API base URL, e.g. `http://localhost:5000`.
 */
function getApiUrl(): string {
  const config = useRuntimeConfig()
  return (config.apiUrl as string) || 'http://localhost:5000'
}

/**
 * Attempt to refresh the access token using the stored refresh token.
 *
 * Calls the SSO token endpoint with grant_type=refresh_token. On success,
 * updates all auth cookies and returns the new access token. On failure,
 * clears all cookies and returns null.
 *
 * Uses a module-level promise lock so concurrent 401s share a single refresh call.
 *
 * @param event - H3 event (needed to read/write cookies on the browser response)
 * @returns The new access token string, or null if refresh failed.
 */
export async function tryRefreshToken(event: H3Event): Promise<string | null> {
  if (refreshPromise) return refreshPromise

  const refreshToken = getCookie(event, 'refresh_token')
  if (!refreshToken) {
    clearAllAuthCookies(event)
    return null
  }

  refreshPromise = (async () => {
    try {
      const config = useRuntimeConfig()
      const response = await $fetch<{
        access_token: string
        refresh_token?: string
        expires_in?: number
      }>(`${config.ssoUrl}/connect/token`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({
          grant_type: 'refresh_token',
          refresh_token: refreshToken,
          client_id: config.ssoClientId,
          client_secret: config.ssoClientSecret,
        }).toString(),
      })

      const { access_token, refresh_token: newRefreshToken, expires_in } = response
      if (!access_token) {
        clearAllAuthCookies(event)
        return null
      }

      setCookie(event, 'auth_token', access_token, {
        httpOnly: true,
        secure: process.env.NODE_ENV === 'production',
        sameSite: 'lax',
        maxAge: expires_in ?? 60 * 15,
        path: '/',
      })

      if (newRefreshToken) {
        setCookie(event, 'refresh_token', newRefreshToken, {
          httpOnly: true,
          secure: process.env.NODE_ENV === 'production',
          sameSite: 'lax',
          maxAge: 60 * 60 * 24 * 7,
          path: '/',
        })
      }

      return access_token
    } catch {
      clearAllAuthCookies(event)
      return null
    }
  })()

  try {
    return await refreshPromise
  } finally {
    refreshPromise = null
  }
}

/**
 * Make an authenticated request to the internal C# backend API.
 *
 * On 401 responses, automatically attempts a token refresh and retries
 * the request once. If refresh fails, throws 401 to the client.
 *
 * @param event - H3 event from the Nuxt server route handler
 * @param endpoint - API path after `/api`, e.g. `/clients/me`
 * @param options - Optional method and body
 * @returns Parsed JSON response typed as `T`
 * @throws H3Error forwarding any upstream HTTP error status
 */
export async function internalApiCall<T>(
  event: H3Event,
  endpoint: string,
  options: {
    method?: 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH'
    body?: unknown
    headers?: Record<string, string>
  } = {}
): Promise<T> {
  const apiUrl = getApiUrl()
  const token = getCookie(event, 'auth_token')

  const buildHeaders = (bearerToken: string | undefined): Record<string, string> => {
    const h: Record<string, string> = {
      'Content-Type': 'application/json',
      ...options.headers,
    }
    if (bearerToken) {
      h['Authorization'] = `Bearer ${bearerToken}`
    }
    return h
  }

  const makeRequest = (bearerToken: string | undefined): Promise<T> =>
    $fetch<T>(`${apiUrl}/api${endpoint}`, {
      method: options.method ?? 'GET',
      headers: buildHeaders(bearerToken),
      body: options.body,
    })

  try {
    return await makeRequest(token)
  } catch (err: unknown) {
    const fetchErr = err as { status?: number; statusCode?: number; data?: unknown; message?: string }
    const status = fetchErr?.status ?? fetchErr?.statusCode ?? 502

    // On 401: attempt token refresh and retry (skip for auth endpoints to avoid loops)
    const isAuthEndpoint = endpoint.startsWith('/auth/')
    if (status === 401 && !isAuthEndpoint) {
      const newToken = await tryRefreshToken(event)
      if (newToken) {
        try {
          return await makeRequest(newToken)
        } catch (retryErr: unknown) {
          const retryFetchErr = retryErr as { status?: number; statusCode?: number; data?: unknown; message?: string }
          const retryStatus = retryFetchErr?.status ?? retryFetchErr?.statusCode ?? 502
          const retryData = retryFetchErr?.data as { error?: string; message?: string } | undefined
          const retryMessage = retryData?.error
            ?? retryData?.message
            ?? retryFetchErr?.message
            ?? `Backend API error for ${endpoint}`

          throw createError({ statusCode: retryStatus, statusMessage: retryMessage })
        }
      }
    }

    const data = fetchErr?.data as { error?: string; message?: string } | undefined
    const message = data?.error
      ?? data?.message
      ?? fetchErr?.message
      ?? `Backend API error for ${endpoint}`

    throw createError({ statusCode: status, statusMessage: message })
  }
}

/**
 * Set the JWT access token cookie on the response.
 *
 * MaxAge is 15 minutes to match the backend JWT lifetime.
 * Also sets the `authed` flag cookie (7 days, non-httpOnly) so client-side
 * middleware can check login status without reading the token.
 *
 * @param event - H3 event
 * @param token - JWT access token from the C# API
 */
export function setAuthCookie(event: H3Event, token: string): void {
  setCookie(event, 'auth_token', token, {
    httpOnly: true,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'lax',
    maxAge: 60 * 15, // 15 minutes -- matches JWT expiry
    path: '/',
  })

  setCookie(event, 'authed', '1', {
    httpOnly: false,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'lax',
    maxAge: 60 * 60 * 24 * 7, // 7 days -- matches refresh token lifetime
    path: '/',
  })
}

/**
 * Set the refresh token cookie on the response.
 *
 * httpOnly and Secure in production. 7-day lifetime to match the
 * backend's refresh token expiry.
 *
 * @param event - H3 event
 * @param token - Opaque refresh token from the C# API
 */
export function setRefreshCookie(event: H3Event, token: string): void {
  setCookie(event, 'refresh_token', token, {
    httpOnly: true,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'lax',
    maxAge: 60 * 60 * 24 * 7, // 7 days
    path: '/',
  })
}

/**
 * Clear all auth-related cookies (access token, refresh token, and flag).
 *
 * @param event - H3 event
 */
export function clearAllAuthCookies(event: H3Event): void {
  deleteCookie(event, 'auth_token', { path: '/' })
  deleteCookie(event, 'refresh_token', { path: '/' })
  deleteCookie(event, 'authed', { path: '/' })
}
