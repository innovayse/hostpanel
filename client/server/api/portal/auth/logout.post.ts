/**
 * POST /api/portal/auth/logout
 *
 * Called by the shared header widget when the user clicks Sign out.
 * 1. Revokes the refresh token on the hostpanel backend.
 * 2. Fires backchannel logout to all platform services via app.local
 *    (app.local already knows all internal service URLs and keys).
 * 3. Clears all auth cookies.
 */
import { clearAllAuthCookies } from '~/server/utils/api'

export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig()
  const refreshToken = getCookie(event, 'refresh_token')
  const authToken = getCookie(event, 'auth_token')

  // 1. Revoke refresh token on hostpanel backend
  try {
    const apiUrl = (config.apiUrl as string) || 'http://localhost:5000'
    await $fetch(`${apiUrl}/api/auth/logout`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(refreshToken ? { Cookie: `refreshToken=${refreshToken}` } : {}),
      },
    })
  } catch {
    // Ignore — clear cookies regardless
  }

  // 2. Fire backchannel logout to all platform services via app.local.
  // The widget goes to SSO endsession after this returns, but backchannel
  // calls are fire-and-forget so we don't block the response on them.
  if (authToken) {
    try {
      const payload = authToken.split('.')[1] ?? ''
      const decoded = JSON.parse(Buffer.from(payload, 'base64url').toString('utf8'))
      const email: string = decoded.email || decoded.sub || ''
      const mainApiUrl = (config.mainApiUrl as string) || 'http://main-client:3000'
      if (email) {
        fetch(`${mainApiUrl}/api/portal/auth/sso/logout-redirect?redirect_uri=${encodeURIComponent('http://panel.local')}`, {
          headers: { Cookie: `auth_token=${authToken}` },
          redirect: 'manual',
          signal: AbortSignal.timeout(3000),
        }).catch(() => {})
      }
    } catch { /* ignore */ }
  }

  // 3. Clear all cookies
  clearAllAuthCookies(event)
  return { ok: true }
})
