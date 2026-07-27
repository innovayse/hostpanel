import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { useApi, clearToken } from '../../../composables/useApi'
import { startSsoLogin, ssoLogoutRedirect } from '../useSso'

interface MeResponse {
  email: string
  roles: string[]
  emailVerified: boolean
}

/**
 * Pinia store managing admin authentication state.
 *
 * All authentication happens against Innovayse SSO (see useSso.ts) — this
 * store only tracks the resulting session (who's logged in, their roles,
 * email verification status) by calling the Hostpanel API's /auth/me, since
 * roles are assigned locally in Hostpanel and can't be read off the SSO token.
 */
export const useAuthStore = defineStore('auth', () => {
  /** Currently authenticated admin user, null when unauthenticated. */
  const user = ref<{ email: string; roles: string[] } | null>(null)

  /** Whether the current user's email has been verified. Null means not yet checked. */
  const emailVerified = ref<boolean | null>(null)

  const { request } = useApi()

  /** True when a user session is active. */
  const isAuthenticated = computed(() => user.value !== null)

  /**
   * Redirects the browser to Innovayse SSO to start the login flow.
   * On success, SSO redirects back to /auth/callback, which exchanges the
   * code for tokens and calls fetchMe() to populate this store.
   */
  async function login(): Promise<void> {
    await startSsoLogin()
  }

  /**
   * Loads the current user from the API using the stored access token.
   * Called after the SSO callback exchange, and on page load to restore
   * an existing session.
   *
   * @returns Promise that resolves when the fetch completes; clears session on failure.
   */
  async function fetchMe(): Promise<void> {
    if (!localStorage.getItem('admin_token')) {
      user.value = null
      emailVerified.value = null
      return
    }

    try {
      const data = await request<MeResponse>('/auth/me')
      user.value = { email: data.email, roles: data.roles }
      emailVerified.value = data.emailVerified
    } catch {
      clearToken()
      user.value = null
      emailVerified.value = null
    }
  }

  /**
   * Ends the local session and redirects to SSO's end-session endpoint so
   * the SSO-wide session is cleared too (not just this app's token).
   */
  function logout(): void {
    clearToken()
    user.value = null
    emailVerified.value = null
    ssoLogoutRedirect()
  }

  return { user, emailVerified, isAuthenticated, login, logout, fetchMe }
})
