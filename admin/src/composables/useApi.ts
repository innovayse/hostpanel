/**
 * Fetch wrapper for the Hostpanel API — the only layer that knows about transport.
 *
 * Under `AUTH_MODE=sso` it holds no credential at all: the API performs the OIDC
 * exchange itself and hands the browser an opaque, httpOnly session cookie, which the
 * browser attaches on its own and the server renews behind. (This file used to keep an
 * access token AND a refresh token in localStorage — a 30-day credential readable by
 * any script on the page — plus its own refresh loop. That is gone and is not coming
 * back.)
 *
 * Under `AUTH_MODE=local` it now holds no credential either. `POST /api/auth/login`
 * still mints a short-lived (15 minute) JWT, but the API writes the browser's copy into
 * an httpOnly `hostpanel_session` cookie and reads it back from there when no
 * `Authorization` header is present. This file used to park that token in
 * `sessionStorage` — readable by any script injected into the page — which contradicted
 * the decision the SSO path had already made for the same reason. Memory-only storage
 * was not the alternative: it signs the operator out on every refresh, which is why
 * `sessionStorage` was reached for in the first place.
 *
 * So there is no token slot here any more, in either mode. Whether a session exists is a
 * question only the server can answer, which is what `/auth/me` is for.
 */

/**
 * Header a cross-site page cannot set without a CORS preflight the API does not
 * grant. The API refuses any state-changing request that arrives without it.
 */
const CSRF_HEADER = { 'X-Requested-With': 'XMLHttpRequest' } as const

/**
 * An HTTP failure that kept its response body, so callers can read the reason the
 * server actually gave instead of inventing one.
 */
export class ApiError extends Error {
  /**
   * @param status - HTTP status code the API answered with.
   * @param body - Parsed JSON body, or null when the response carried none.
   */
  constructor(
    /** HTTP status code the API answered with. */
    public readonly status: number,
    /** Parsed JSON body of the failed response, null when there was none. */
    public readonly body: unknown,
  ) {
    super(`API error: ${status}`)
    this.name = 'ApiError'
  }
}

/**
 * Sends the browser to sign in through the SSO, returning to the current page
 * afterwards. Meaningful only under `AUTH_MODE=sso`, where `/api/auth/login` is a
 * redirect endpoint; in local mode that same path is a JSON POST endpoint instead,
 * which is exactly why the login page has to ask for the mode first.
 *
 * @returns Nothing; navigates the document away.
 */
export const redirectToLogin = (): void => {
  const returnTo = location.pathname + location.search
  location.href = `/api/auth/login?returnTo=${encodeURIComponent(returnTo)}`
}

/**
 * Ends the session — this product's, or every product's with scope 'all' — and
 * follows the SSO's end-session URL so the next sign-in asks for credentials.
 *
 * @param scope - 'this' ends only Hostpanel's session, 'all' ends every product's.
 * @returns Nothing; navigates the document away.
 */
export const logoutSession = async (scope: 'this' | 'all' = 'this'): Promise<void> => {
  const res = await fetch(`/api/auth/logout${scope === 'all' ? '?scope=all' : ''}`, {
    method: 'POST',
    headers: { ...CSRF_HEADER },
  })
  const endSessionUrl = res.ok ? (await res.json().catch(() => null))?.endSessionUrl : null
  location.href = endSessionUrl ?? '/'
}

/** Composable for making authenticated API calls via the same-origin proxy. */
export const useApi = () => {
  /**
   * Makes an API request to the C# backend.
   *
   * @param endpoint - API path (e.g. '/admin/dashboard/stats').
   * @param options - Native fetch options to merge.
   * @returns Parsed JSON response data.
   * @throws {ApiError} When the API answers with a non-2xx status. The response body
   * travels on the error so the caller can read the server's own wording.
   */
  const request = async <T>(endpoint: string, options: RequestInit = {}): Promise<T> => {
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
    if (!response.ok) {
      // The body is read here rather than discarded: every auth failure the API
      // reports — bad password, bad TOTP code, setup already claimed — explains
      // itself in `{ error: "…" }`, and a page that cannot see it has no choice but
      // to make a sentence up.
      const failureText = await response.text().catch(() => '')
      let failureBody: unknown = null
      try {
        failureBody = failureText ? JSON.parse(failureText) : null
      } catch {
        // Not JSON — a proxy error page, most likely. The status still means something.
        failureBody = null
      }
      throw new ApiError(response.status, failureBody)
    }

    if (response.status === 204) return undefined as T

    const text = await response.text()
    if (!text) return undefined as T

    return JSON.parse(text) as T
  }

  return { request }
}
