/**
 * Client area route middleware
 *
 * Protects all /client/* routes except /client/login and /client/register.
 * Uses the `whmcs_authed` cookie (non-httpOnly) which is readable on both
 * server and client, avoiding SSR/hydration mismatches.
 *
 * Applied via `definePageMeta({ middleware: 'client-auth' })` on each page.
 */
export default defineNuxtRouteMiddleware((to) => {
  // useCookie works on server (reads from request headers) AND client (document.cookie)
  // whmcs_authed is non-httpOnly so it's accessible in both environments
  const authed = useCookie('authed')

  const publicRoutes = [
    '/client/login',
    '/client/register',
    '/client/forgot-password',
    '/client/reset-password',
    '/client/confirm-email',
  ]
  const isPublic = publicRoutes.some(r => to.path === r || to.path.startsWith(r + '/'))

  const config = useRuntimeConfig()

  // Not logged in
  if (!authed.value) {
    if (config.public.authMode === 'sso') {
      // SSO mode: always go straight to SSO (skips "Continue with Innovayse" page).
      // The SSO decides: show login (no session) or chooser (has accounts).
      if (to.path !== '/api/portal/auth/sso/authorize') {
        const baseUrl = config.public.baseUrl || 'http://localhost:3001'
        return navigateTo(`${baseUrl}/api/portal/auth/sso/authorize`, { external: true, replace: true })
      }
    } else if (!isPublic) {
      // Local mode: redirect to login page
      return navigateTo(`/client/login?redirect=${encodeURIComponent(to.fullPath)}`)
    }
    return
  }

  // Already logged in + trying to access login/register → redirect to dashboard
  if (isPublic) {
    return navigateTo('/client/dashboard')
  }
})
