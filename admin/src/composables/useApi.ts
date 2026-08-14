/**
 * Fetch wrapper for the Hostpanel API. It holds no credential of any kind.
 *
 * This file used to keep an access token AND a refresh token in localStorage — a
 * 30-day credential readable by any script on the page — plus its own refresh loop.
 * All of it is gone: the API performs the OIDC exchange itself and hands the browser
 * an opaque, httpOnly session cookie, which the browser attaches on its own and the
 * server renews behind.
 */

/**
 * Header a cross-site page cannot set without a CORS preflight the API does not
 * grant. The API refuses any state-changing request that arrives without it.
 */
const CSRF_HEADER = { 'X-Requested-With': 'XMLHttpRequest' } as const

/**
 * Sends the browser to sign in, returning to the current page afterwards.
 */
export function redirectToLogin(): void {
  const returnTo = location.pathname + location.search
  location.href = `/api/auth/login?returnTo=${encodeURIComponent(returnTo)}`
}

/**
 * Ends the session — this product's, or every product's with scope 'all' — and
 * follows the SSO's end-session URL so the next sign-in asks for credentials.
 */
export async function logoutSession(scope: 'this' | 'all' = 'this'): Promise<void> {
  const res = await fetch(`/api/auth/logout${scope === 'all' ? '?scope=all' : ''}`, {
    method: 'POST',
    headers: { ...CSRF_HEADER },
  })
  const endSessionUrl = res.ok ? (await res.json().catch(() => null))?.endSessionUrl : null
  location.href = endSessionUrl ?? '/'
}

/** Composable for making authenticated API calls via the same-origin proxy. */
export function useApi() {
  /**
   * Makes an API request to the C# backend.
   *
   * @param endpoint - API path (e.g. '/admin/dashboard/stats').
   * @param options - Native fetch options to merge.
   * @returns Parsed JSON response data.
   */
  async function request<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
    const response = await fetch(`/api${endpoint}`, {
      ...options,
      headers: {
        'Content-Type': 'application/json',
        ...CSRF_HEADER,
        ...(options.headers as Record<string, string> ?? {}),
      },
    })

    // A 401 is thrown like any other error and nothing is redirected from here.
    //
    // The old code pushed to /login on 401, which was survivable only because it never
    // reached the API without a token in hand. fetchMe now always asks the server —
    // that is the whole point, the browser can no longer answer "am I signed in" — so
    // a redirect here fires *inside* the router guard that called it. vue-router
    // aborts the navigation in progress and nothing renders at all: a blank page, no
    // error. The guard already sends unauthenticated visitors to /login; this layer
    // only has to report what happened.
    if (!response.ok) throw new Error(`API error: ${response.status}`)

    if (response.status === 204) return undefined as T

    const text = await response.text()
    if (!text) return undefined as T

    return JSON.parse(text) as T
  }

  return { request }
}
