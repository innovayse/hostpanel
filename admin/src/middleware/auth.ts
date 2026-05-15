import type { RouteLocationNormalized } from 'vue-router'
import { useAuthStore } from '../modules/auth/stores/authStore'
import { pinia } from '../pinia'

/** LocalStorage key used to track whether initial admin setup is complete. */
const SETUP_COMPLETE_KEY = 'admin_setup_complete'

/**
 * Marks the initial admin setup as complete.
 *
 * Stores a flag in localStorage so subsequent page loads skip the setup route.
 */
export function markSetupComplete(): void {
  localStorage.setItem(SETUP_COMPLETE_KEY, 'true')
}

/**
 * Auth middleware — protects routes that require authentication.
 *
 * Restores session from localStorage JWT on first load.
 * Redirects unauthenticated users to /login.
 * Redirects authenticated users away from /login to /dashboard.
 *
 * @param to - Target route being navigated to.
 * @returns The redirect path or true to allow navigation.
 */
export async function authMiddleware(
  to: RouteLocationNormalized,
): Promise<string | boolean> {
  const auth = useAuthStore(pinia)

  // Restore session from stored token on first load
  if (auth.user === null) {
    await auth.fetchMe()
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
