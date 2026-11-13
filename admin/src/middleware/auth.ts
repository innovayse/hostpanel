/** Navigation guard for the admin router: loads the session state and redirects by route meta. */
import type { RouteLocationNormalized } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { AuthMode } from '@/types/auth'
import { pinia } from '@/pinia'
import { AppRoute } from '@/lib/routes'
import { REDIRECT_QUERY_PARAM, loginLocationFor, safeRedirectTarget } from '@/lib/redirectTarget'

/**
 * Auth middleware: decides, per navigation, whether a route may be entered.
 *
 * Everything it decides on comes from the server. Whether setup is complete is asked of
 * `/auth/setup-required` rather than kept in browser storage, which a second machine, a
 * cleared profile or a private window would not share.
 *
 * @param to - Target route being navigated to.
 * @returns The redirect path, or true to allow the navigation.
 */
export const authMiddleware = async (
  to: RouteLocationNormalized,
): Promise<string | boolean> => {
  const auth = useAuthStore(pinia)

  // Which mode this deployment runs, whether anyone holds Admin yet, and whether a session exists.
  // The three are independent questions only the server can answer (the session credential is an
  // httpOnly cookie the page cannot read), so they go out together: asking them one after the other
  // cost a full round trip each before the first screen could open. All are asked once per page
  // load rather than per navigation; afterwards the guard reads the store and makes no request. The
  // session is asked through `checkSession`, which remembers a signed-out answer as well, so an
  // anonymous visitor is not probed again on every navigation.
  await Promise.all([
    auth.mode === null ? auth.loadMode() : Promise.resolve(),
    auth.checkSession(),
  ])

  // The first-run screen. It creates the very first local account, so it is meaningful
  // only while nobody holds Admin and only where this deployment owns its own accounts.
  // On an SSO deployment there is nothing for it to create, the accounts live in the
  // sign-on service, and reaching it would call endpoints that answer 404 there.
  if (to.meta.setup === true) {
    if (!auth.setupRequired || auth.mode !== AuthMode.Local) {
      return AppRoute.Login
    }
    return true
  }

  // A signed-in user on a public page (the sign-in screen, or the page an SSO round trip returns
  // to) goes where they were originally heading, if that target is a safe in-app path.
  // Only an Admin is sent on: anyone else stays on the sign-in page, which tells them so.
  if (to.meta.public && auth.isAuthenticated && auth.isAdmin) {
    return safeRedirectTarget(to.query[REDIRECT_QUERY_PARAM]) ?? AppRoute.Dashboard
  }

  // Protected route without a valid session → sign in first, remembering the full requested path
  if (to.meta.requiresAuth && !auth.isAuthenticated) {
    return loginLocationFor(to.fullPath)
  }

  // Signed in without the Admin role (a customer through the shared sign-on): the API refuses
  // every admin call anyway, but the panel must not open its screens either: the access-denied screen says so.
  if (to.meta.requiresAuth && !auth.isAdmin) {
    return AppRoute.Forbidden
  }

  return true
}
