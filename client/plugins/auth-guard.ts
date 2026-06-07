/**
 * Global auth guard plugin — runs on CLIENT SIDE ONLY
 *
 * Provides a global `$handleAuthExpired()` function that clears all
 * client-side auth state and redirects to the login page.
 *
 * Called from `apiFetch` when a 401 reaches the client (meaning the
 * server-side token refresh also failed — the session is truly expired).
 *
 * Uses a `provide` pattern rather than `app:error` or `vue:error` hooks
 * because those hooks don't reliably catch `$fetch` errors from Pinia stores.
 * Instead, `apiFetch` (the single gateway for all client API calls) checks
 * for 401 and calls `$handleAuthExpired()` directly.
 */
import { useClientStore } from '~/stores/client'

export default defineNuxtPlugin(() => {
  if (!import.meta.client) return

  return {
    provide: {
      handleAuthExpired: () => {
        try {
          const store = useClientStore()
          store.reset()
        } catch {
          // Store may not be initialized
        }

        const authedCookie = useCookie('authed')
        authedCookie.value = null

        return navigateTo('/client/login')
      },
    },
  }
})
