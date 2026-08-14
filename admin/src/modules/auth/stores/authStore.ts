import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { useApi, redirectToLogin, logoutSession } from '../../../composables/useApi'

interface MeResponse {
  email: string
  roles: string[]
  emailVerified: boolean
}

/**
 * Pinia store managing admin authentication state.
 *
 * Authentication happens against Innovayse SSO, but no longer in this browser: the
 * Hostpanel API performs the OIDC exchange and holds the tokens, and this store only
 * tracks the resulting session by calling /auth/me — roles are assigned locally in
 * Hostpanel and cannot be read off any SSO token anyway.
 *
 * The PKCE flow, the callback view and the refresh loop that used to live beside this
 * store are gone with the tokens they existed to manage.
 */
export const useAuthStore = defineStore('auth', () => {
  /** Currently authenticated admin user, null when unauthenticated. */
  const user = ref<{ email: string; roles: string[] } | null>(null)

  /** Whether the current user's email has been verified. Null means not yet checked. */
  const emailVerified = ref<boolean | null>(null)

  const { request } = useApi()

  /** True when a user session is active. */
  const isAuthenticated = computed(() => user.value !== null)

  /** Sends the browser to sign in; the SSO returns to the API's own callback. */
  function login(): void {
    redirectToLogin()
  }

  /**
   * Loads the current user from the API. There is no token to check first — whether a
   * session exists is a question only the server can answer now.
   *
   * @returns Promise that resolves when the fetch completes; clears state on failure.
   */
  async function fetchMe(): Promise<void> {
    try {
      const data = await request<MeResponse>('/auth/me')
      user.value = { email: data.email, roles: data.roles }
      emailVerified.value = data.emailVerified
    } catch {
      user.value = null
      emailVerified.value = null
    }
  }

  /** Ends the session server-side and at the SSO, so nothing survives to revive it. */
  async function logout(): Promise<void> {
    user.value = null
    emailVerified.value = null
    await logoutSession()
  }

  return { user, emailVerified, isAuthenticated, login, logout, fetchMe }
})
