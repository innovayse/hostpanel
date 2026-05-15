/**
 * C# backend API utility — server-side only
 *
 * All backend API calls go through `internalApiCall`. It:
 * - Reads `apiUrl` from runtimeConfig (set via API_URL env var)
 * - Attaches the JWT token from the `auth_token` httpOnly cookie
 * - Throws appropriate H3 errors on failure
 *
 * Usage:
 * ```ts
 * const data = await internalApiCall<ClientDto>(event, '/clients/me')
 * ```
 */

import type { H3Event } from 'h3'

/**
 * Make an authenticated request to the internal C# backend API.
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
  } = {}
): Promise<T> {
  const config = useRuntimeConfig()
  const apiUrl = (config.apiUrl as string) || 'http://localhost:5000'
  const token = getCookie(event, 'auth_token')

  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
  }

  if (token) {
    headers['Authorization'] = `Bearer ${token}`
  }

  try {
    return await $fetch<T>(`${apiUrl}/api${endpoint}`, {
      method: options.method ?? 'GET',
      headers,
      body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
    })
  } catch (err: unknown) {
    const fetchErr = err as { status?: number; statusCode?: number; data?: unknown; message?: string }
    const status = fetchErr?.status ?? fetchErr?.statusCode ?? 502
    const data = fetchErr?.data as { error?: string; message?: string } | undefined
    const message = data?.error
      ?? data?.message
      ?? fetchErr?.message
      ?? `Backend API error for ${endpoint}`

    throw createError({ statusCode: status, statusMessage: message })
  }
}

/**
 * Set the JWT auth cookie on the response.
 *
 * @param event - H3 event
 * @param token - JWT access token from the C# API
 */
export function setAuthCookie(event: H3Event, token: string): void {
  setCookie(event, 'auth_token', token, {
    httpOnly: true,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'lax',
    maxAge: 60 * 60 * 24 * 7, // 7 days
    path: '/',
  })

  // Readable flag cookie — lets client-side JS and middleware know user is logged in
  setCookie(event, 'authed', '1', {
    httpOnly: false,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'lax',
    maxAge: 60 * 60 * 24 * 7,
    path: '/',
  })
}

/**
 * Clear the JWT auth cookie and the readable auth flag cookie.
 *
 * @param event - H3 event
 */
export function clearAuthCookie(event: H3Event): void {
  deleteCookie(event, 'auth_token', { path: '/' })
  deleteCookie(event, 'authed', { path: '/' })
}
