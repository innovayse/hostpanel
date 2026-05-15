/**
 * Client authentication composable
 *
 * Auth strategy:
 * - `auth_token`  — httpOnly JWT cookie, set server-side after login
 * - `authed`      — plain cookie (value "1"), readable by JS and middleware on both sides
 *
 * `useState('client_user')` is used for user data so state is:
 *   - scoped per request on the server (no cross-request leakage)
 *   - hydrated to the client automatically by Nuxt
 */

export function useClientAuth() {
  /** User data — SSR-safe via useState, hydrated to client */
  const user = useState<ClientUser | null>('client_user', () => null)

  /**
   * Readable auth flag cookie. Set to "1" on login, deleted on logout.
   * Not httpOnly so middleware and client JS can both read it.
   */
  const authedCookie = useCookie<string | null>('authed', { readonly: true })

  /** True when the user is logged in (checks the readable flag cookie) */
  const isLoggedIn = computed(() => !!(authedCookie.value || user.value))

  /**
   * Fetch and cache current user data from the server.
   * Safe to call multiple times — skips if user is already loaded.
   */
  async function fetchUser() {
    if (user.value) return
    try {
      const data = await apiFetch<ClientUser>('/api/portal/client/me')
      user.value = data
    } catch {
      user.value = null
    }
  }

  /**
   * Log in with email + password.
   * Sets both cookies server-side and updates local user state.
   */
  async function login(email: string, password: string) {
    const data = await apiFetch<ClientUser>('/api/portal/auth/login', {
      method: 'POST',
      body: { email, password }
    })
    user.value = data
    return data
  }

  /**
   * Log out the current user.
   * Clears both cookies server-side and resets local state.
   */
  async function logout() {
    await apiFetch('/api/portal/auth/logout', { method: 'POST' })
    user.value = null
  }

  /**
   * Register a new WHMCS client account.
   * Does not log the user in — redirect to login after.
   */
  async function register(payload: {
    firstname: string
    lastname: string
    email: string
    password: string
  }) {
    return apiFetch('/api/portal/auth/register', {
      method: 'POST',
      body: payload
    })
  }

  return {
    user: readonly(user),
    isLoggedIn,
    fetchUser,
    login,
    logout,
    register
  }
}
