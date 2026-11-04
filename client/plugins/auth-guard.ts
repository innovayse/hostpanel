/**
 * Global auth guard plugin: runs on CLIENT SIDE ONLY
 *
 * Provides a global `$handleAuthExpired()` function that clears all
 * client-side auth state and redirects to the login page, carrying the page the customer was
 * on as `?redirect=` so signing in again returns them there.
 *
 * Called from `apiFetch` when a 401 reaches the client (meaning the
 * server-side token refresh also failed: the session is truly expired). Off the signed-in area
 * the state is still cleared but the visitor stays put.
 *
 * Uses a `provide` pattern rather than `app:error` or `vue:error` hooks
 * because those hooks don't reliably catch `$fetch` errors from Pinia stores.
 * Instead, `apiFetch` (the single gateway for all client API calls) checks
 * for 401 and calls `$handleAuthExpired()` directly.
 *
 * @module plugins/auth-guard
 */

import { useClientStore } from '~/stores/client'
import { usePluginsStore } from '~/stores/plugins'
import { isPrivateClientPath, loginPathWithReturn } from '~/utils/returnPath'
import { SessionCookie } from '~/constants/sessionCookie'

/** Provides `$handleAuthExpired` in the browser; a no-op on the server. */
export default defineNuxtPlugin(() => {
  // `import.meta.server` rather than `!import.meta.client`: identical in a Nuxt build, but a unit
  // test (where neither flag is defined) still reaches the browser branch.
  if (import.meta.server) {
    return
  }

  // Captured while the plugin runs: it is the Nuxt context a later call from a store may lack.
  const router = useRouter()

  return {
    provide: {
      handleAuthExpired: () => {
        try {
          const store = useClientStore()
          store.reset()
          // The plugin index belongs to the person whose session ended; the next one reads their own.
          usePluginsStore().reset()
        } catch {
          // Store may not be initialized
        }

        const authedCookie = useCookie(SessionCookie.Authed)
        authedCookie.value = null

        // Only a page that needs a session is left behind. A 401 on a public page is either a
        // refused credential or code on the sign-in form (navigating anyway would rebuild the URL
        // without its `?redirect=`, so a customer who mistyped a password would be returned to
        // the dashboard instead of the page they had come for) or the identity read the header
        // makes on every page for a visitor whose `authed` cookie outlived the session, which
        // would bounce the home page, the catalogue and the cart to the sign-in form.
        if (!isPrivateClientPath(router.currentRoute.value.path)) {
          return
        }

        // The page the 401 interrupted, so the sign-in form can send the customer back to it.
        return navigateTo(loginPathWithReturn(router.currentRoute.value.fullPath))
      },
    },
  }
})
