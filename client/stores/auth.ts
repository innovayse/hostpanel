/**
 * Pinia store for who is signed in.
 *
 * Auth strategy (unchanged from the composable this replaced):
 * - `auth_token` — httpOnly JWT cookie, set server-side after login
 * - `authed`     — plain cookie (value "1"), readable by JS and middleware on both sides
 *
 * A setup store rather than an options store because {@link isLoggedIn} has to read a cookie
 * ref, and `useCookie` must be called once inside the Nuxt context rather than per getter
 * evaluation. Pinia hydrates the state across SSR the same way `useState` did, so there is no
 * cross-request leakage and no extra fetch on the client.
 *
 * URLs and transport belong to {@link useAuthApi}; this store owns only the state.
 *
 * @module stores/auth
 */

import { defineStore } from 'pinia'
import { useAuthApi } from '~/composables/apis/useAuthApi'
import { useClientStore } from '~/stores/client'
import type { ClientUser } from '~/types/clientuser'
import type { LoginResult } from '~/types/loginresult'
import type { RegisterPayload } from '~/types/registerpayload'

/**
 * Central store for the signed-in identity.
 *
 * @returns The auth state and the actions that change it.
 */
export const useAuthStore = defineStore('auth', () => {
  const api = useAuthApi()

  /** The signed-in account, or null when nobody is signed in or the profile is not loaded yet. */
  const user = ref<ClientUser | null>(null)

  /**
   * Readable auth flag cookie. Set to "1" on login, deleted on logout.
   * Not httpOnly so middleware and client JS can both read it.
   */
  const authedCookie = useCookie<string | null>('authed', { readonly: true })

  /** True when the user is logged in (checks the readable flag cookie). */
  const isLoggedIn = computed(() => !!(authedCookie.value || user.value))

  /**
   * Fetch and cache current user data from the server.
   * Safe to call multiple times — skips if user is already loaded.
   *
   * Delegates to {@link useClientStore} rather than calling `/client/me` itself. Both stores
   * wanted the same record and each fetched it separately, so a client-area page load asked
   * for the identity once here and again there — `apiFetch` is `$fetch`-based and dedupes
   * nothing. A store calling another store is the sanctioned direction; a second store
   * calling the same API composable was the smell. The dedupe lives one layer down, in the
   * store that owns the record.
   *
   * @returns Nothing; a failure leaves {@link user} null rather than throwing — the client
   * store keeps the reason (or, for an identity with no client record, its own flag).
   */
  const fetchUser = async (): Promise<void> => {
    if (user.value) return
    const clientStore = useClientStore()
    await clientStore.fetchUser()
    user.value = clientStore.user
  }

  /**
   * Log in with email + password.
   *
   * @param email - Account email.
   * @param password - Account password.
   * @returns The second-factor challenge when TOTP is on — no cookies are set and {@link user}
   * is left alone in that case — otherwise the signed-in account.
   * @throws Whatever the API composable throws when the credentials are rejected.
   */
  const login = async (email: string, password: string): Promise<LoginResult> => {
    const data = await api.login(email, password)
    // The `in` check is the whole discriminant -- `twoFactorRequired` is typed as the literal
    // `true`, so its presence is what tells the two answers apart. Reading the property as well
    // (`&& data.twoFactorRequired`) made the condition a conjunction, which TypeScript cannot
    // use to narrow the *else* branch: `user.value = data` below was then assigning a
    // `LoginResult`, challenge included, into a `ClientUser | null` ref.
    if ('twoFactorRequired' in data) {
      return data
    }
    user.value = data
    return data
  }

  /**
   * Complete 2FA login with a TOTP code.
   * Sets auth cookies server-side and updates local user state.
   *
   * @param pendingToken - Token the challenge answered with.
   * @param code - TOTP code the visitor read off their authenticator.
   * @returns The signed-in account.
   * @throws Whatever the API composable throws when the code is rejected.
   */
  const loginWithTwoFactor = async (pendingToken: string, code: string): Promise<ClientUser> => {
    const data = await api.loginWithTwoFactor(pendingToken, code)
    user.value = data
    return data
  }

  /**
   * Log out the current user.
   * Clears both cookies server-side and resets local state.
   * Mode-aware: SSO logout hits the SSO endsession endpoint; local logout invalidates the refresh token.
   *
   * @returns Nothing.
   * @throws Whatever the API composable throws; local state is only cleared on success, as before.
   */
  const logout = async (): Promise<void> => {
    const config = useRuntimeConfig()
    const authMode = config.public.authMode as string

    if (authMode === 'sso') {
      // SSO logout — clears cookies and redirects to SSO endsession
      await api.ssoLogout()
    } else {
      // Local logout — clears cookies and invalidates refresh token
      await api.logout()
    }
    user.value = null
    // Local mode leaves the browser on the same SPA instance, so the client store's cached
    // profile, services and invoices would otherwise outlive the session and greet whoever
    // signs in next.
    useClientStore().reset()
  }

  /**
   * Sign the visitor out of this product and, under SSO, out of the platform.
   *
   * Distinct from {@link logout}, which clears the local cookies with a `fetch`
   * and nothing more. That is not enough in `sso` mode: the end-session request
   * it triggers is a cross-origin redirect the browser follows *without* the
   * SSO's own cookies, so the identity provider keeps its session and the next
   * "Sign in" signs the same person straight back in without asking. The end
   * session endpoint has to be reached as a top-level navigation.
   *
   * Local cookies are cleared first and the reset is unconditional — a failed
   * clear must still end the session on this side rather than leave a header
   * that claims the visitor is still signed in.
   *
   * @returns Nothing; in SSO mode the browser leaves the page before it resolves.
   */
  const signOut = async (): Promise<void> => {
    const config = useRuntimeConfig()

    try {
      await api.logout()
    } catch { /* the session ends on this side either way */ }
    user.value = null
    useClientStore().reset()

    if ((config.public.authMode as string) !== 'sso') {
      await navigateTo('/client/login')
      return
    }

    const ssoPublicUrl = config.public.ssoPublicUrl as string
    const returnTo = encodeURIComponent(window.location.origin)
    window.location.href = `${ssoPublicUrl}/connect/endsession?post_logout_redirect_uri=${returnTo}`
  }

  /**
   * Register a new WHMCS client account.
   * Does not log the user in — redirect to login after.
   *
   * @param payload - The registration fields.
   * @returns Whatever the endpoint answers with.
   * @throws Whatever the API composable throws.
   */
  const register = (payload: RegisterPayload): Promise<unknown> => api.register(payload)

  /**
   * Clear the signed-in identity without calling the server.
   * Used by the auth-guard plugin when a 401 proves the session is already gone.
   *
   * @returns Nothing.
   */
  const reset = (): void => {
    user.value = null
    useClientStore().reset()
  }

  return {
    user,
    isLoggedIn,
    fetchUser,
    login,
    loginWithTwoFactor,
    logout,
    signOut,
    register,
    reset
  }
})
