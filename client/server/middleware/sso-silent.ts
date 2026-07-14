/**
 * Server middleware — silent SSO
 *
 * Runs on every request before the page renders.
 * If the user has no `authed` cookie and we haven't tried silent SSO yet,
 * redirects to SSO with prompt=none (no login screen shown).
 *
 * SSO responds:
 *   - Active session  → issues code → callback sets authed → back to same page (logged in, no flash)
 *   - No session      → login_required error → callback clears flag → page renders as guest
 */
export default defineEventHandler((event) => {
  // Only intercept page navigations (not API calls, assets, etc.)
  const url = event.path ?? ''
  if (
    url.startsWith('/api/') ||
    url.startsWith('/_nuxt/') ||
    url.startsWith('/__nuxt') ||
    url.includes('.')
  ) return

  const authed = getCookie(event, 'authed')
  const silentTried = getCookie(event, 'sso_silent_tried')

  if (authed || silentTried) return

  // Store the current URL to return to after silent SSO
  setCookie(event, 'sso_silent_return', url || '/', {
    httpOnly: true,
    sameSite: 'lax',
    maxAge: 30,
    path: '/',
  })

  // Mark that we've tried — prevents redirect loop when SSO returns login_required
  setCookie(event, 'sso_silent_tried', '1', {
    httpOnly: true,
    sameSite: 'lax',
    maxAge: 30,
    path: '/',
  })

  return sendRedirect(event, '/api/portal/auth/sso/authorize?prompt=none', 302)
})
