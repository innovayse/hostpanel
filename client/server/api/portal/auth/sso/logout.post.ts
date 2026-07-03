/**
 * POST /api/portal/auth/sso/logout
 *
 * Clears all auth cookies, then redirects to SSO end-session endpoint.
 */
export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig()

  deleteCookie(event, 'auth_token', { path: '/' })
  deleteCookie(event, 'refresh_token', { path: '/' })
  deleteCookie(event, 'authed', { path: '/' })

  const baseUrl = config.public.baseUrl || 'http://localhost:3001'
  const endsessionUrl = `${config.public.ssoPublicUrl}/connect/endsession?post_logout_redirect_uri=${encodeURIComponent(baseUrl)}`
  return sendRedirect(event, endsessionUrl, 302)
})
