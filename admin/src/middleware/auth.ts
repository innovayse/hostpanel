import type { RouteLocationNormalized } from 'vue-router'
import { useAuthStore } from '../modules/auth/stores/authStore'
import { pinia } from '../pinia'

/**
 * Auth middleware — decides, per navigation, whether a route may be entered.
 *
 * Everything it decides on comes from the server. There is no longer a
 * `localStorage` "setup complete" flag: it was written by whichever browser finished
 * setup and read by nothing, so a second machine, a cleared profile or a private window
 * all disagreed with it, and the API answers the same question authoritatively at
 * `/auth/setup-required`.
 *
 * @param to - Target route being navigated to.
 * @returns The redirect path, or true to allow the navigation.
 */
export const authMiddleware = async (
  to: RouteLocationNormalized,
): Promise<string | boolean> => {
  const auth = useAuthStore(pinia)

  // Which mode this deployment runs, and whether anyone holds Admin yet. Both are
  // anonymous, both are needed before /setup can be judged, and both are asked once per
  // page load rather than per navigation.
  if (auth.mode === null) {
    await auth.loadMode()
  }

  // Whether a session exists is a question only the server can answer in either mode —
  // the credential is an httpOnly cookie the page cannot read.
  if (auth.user === null) {
    await auth.fetchMe()
  }

  // The first-run screen. It creates the very first local account, so it is meaningful
  // only while nobody holds Admin and only where this deployment owns its own accounts.
  // On an SSO deployment there is nothing for it to create — the accounts live in the
  // sign-on service — and reaching it would call endpoints that answer 404 there.
  if (to.meta.setup === true) {
    if (!auth.setupRequired || auth.mode !== 'local') return '/login'
    return true
  }

  // Authenticated user trying to access /login → redirect to dashboard
  if (to.meta.public && auth.isAuthenticated) {
    return '/dashboard'
  }

  // Protected route without valid session → redirect to login
  if (to.meta.requiresAuth && !auth.isAuthenticated) {
    return '/login'
  }

  return true
}
