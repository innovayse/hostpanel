import router from '../router'

/** In-memory JWT token — set after login, cleared on logout. */
let _token: string | null = localStorage.getItem('admin_token')

/** Prevents multiple concurrent refresh attempts. */
let _refreshPromise: Promise<boolean> | null = null

/**
 * Stores the JWT token in memory and localStorage for session persistence.
 *
 * @param token - JWT access token from the login response.
 */
export function setToken(token: string): void {
  _token = token
  localStorage.setItem('admin_token', token)
}

/**
 * Clears the stored JWT token from memory and localStorage.
 */
export function clearToken(): void {
  _token = null
  localStorage.removeItem('admin_token')
}

/**
 * Attempts to get a new access token via the httpOnly refresh cookie.
 * Concurrent calls share the same in-flight request.
 *
 * @returns True if a new token was obtained, false otherwise.
 */
async function tryRefresh(): Promise<boolean> {
  if (_refreshPromise) return _refreshPromise

  _refreshPromise = (async () => {
    try {
      const res = await fetch('/api/auth/refresh', { method: 'POST' })
      if (!res.ok) return false
      const data = await res.json()
      if (data.accessToken) {
        setToken(data.accessToken)
        return true
      }
      return false
    } catch {
      return false
    } finally {
      _refreshPromise = null
    }
  })()

  return _refreshPromise
}

/**
 * Composable for making authenticated API calls via the dev proxy.
 *
 * All requests go through /api which is proxied to the C# backend.
 * Attaches the JWT Bearer token from in-memory storage on every request.
 * Automatically refreshes expired tokens using the httpOnly refresh cookie.
 *
 * @returns fetch wrapper with Bearer auth header included.
 */
export function useApi() {
  /**
   * Builds headers and executes a fetch to the given endpoint.
   *
   * @param endpoint - API path (e.g. '/admin/dashboard/stats').
   * @param options - Native fetch options to merge.
   * @returns The raw Response object.
   */
  async function doFetch(endpoint: string, options: RequestInit = {}): Promise<Response> {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      ...(options.headers as Record<string, string> ?? {}),
    }

    if (_token) {
      headers['Authorization'] = `Bearer ${_token}`
    }

    return fetch(`/api${endpoint}`, { ...options, headers })
  }

  /**
   * Makes an API request to the C# backend via proxy.
   *
   * On 401 with an existing token, attempts a silent refresh and retries once.
   * If refresh fails, clears the session and redirects to /login.
   *
   * @param endpoint - API path (e.g. '/admin/dashboard/stats').
   * @param options - Native fetch options to merge.
   * @returns Parsed JSON response data.
   */
  async function request<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
    let response = await doFetch(endpoint, options)

    if (response.status === 401 && _token) {
      const refreshed = await tryRefresh()
      if (refreshed) {
        response = await doFetch(endpoint, options)
      }
    }

    if (!response.ok) {
      if (response.status === 401) {
        clearToken()
        router.push('/login')
      }
      throw new Error(`API error: ${response.status}`)
    }

    if (response.status === 204) return undefined as T

    return response.json() as Promise<T>
  }

  return { request }
}
