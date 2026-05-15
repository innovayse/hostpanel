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

  const publicRoutes = ['/client/login', '/client/register']
  const isPublic = publicRoutes.some(r => to.path === r || to.path.startsWith(r + '/'))

  // Not logged in + trying to access protected route → redirect to login with return path
  if (!isPublic && !authed.value) {
    return navigateTo(`/client/login?redirect=${encodeURIComponent(to.fullPath)}`)
  }

  // Already logged in + trying to access login/register → redirect to dashboard
  if (isPublic && authed.value) {
    return navigateTo('/client/dashboard')
  }
})
