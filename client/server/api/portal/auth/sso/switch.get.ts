import { randomBytes, createHash } from 'node:crypto'

/**
 * GET /api/portal/auth/sso/switch?sub=XXX&redirect=/some/path
 *
 * Switches the active SSO session to a remembered account.
 * Generates a PKCE pair and redirects to accounts.local/switch-init
 * which auto-submits to the SSO switch endpoint, then comes back
 * via the normal OIDC callback with a new auth_token.
 */
export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig()
  const query = getQuery(event)
  const sub = query.sub as string | undefined
  const redirectTo = (query.redirect as string | undefined) || '/client'

  if (!sub) {
    return sendRedirect(event, '/api/portal/auth/sso/authorize', 302)
  }

  // Generate PKCE
  const codeVerifier = randomBytes(32).toString('base64url')
  const codeChallenge = createHash('sha256')
    .update(codeVerifier)
    .digest('base64url')

  setCookie(event, 'pkce_verifier', codeVerifier, {
    httpOnly: true,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'lax',
    maxAge: 60 * 5,
    path: '/',
  })

  const state = randomBytes(16).toString('hex')
  setCookie(event, 'oauth_state', state, {
    httpOnly: true,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'lax',
    maxAge: 60 * 5,
    path: '/',
  })

  setCookie(event, 'post_login_redirect', redirectTo, {
    httpOnly: true,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'lax',
    maxAge: 60 * 5,
    path: '/',
  })

  const redirectUri = config.ssoCallbackUrl
  const oidcParams = new URLSearchParams({
    client_id: config.ssoClientId,
    response_type: 'code',
    redirect_uri: redirectUri,
    scope: 'openid profile email offline_access',
    code_challenge: codeChallenge,
    code_challenge_method: 'S256',
    state,
  })
  const oidcReturnUrl = `/connect/authorize?${oidcParams}`

  // Redirect to accounts.local/switch-init — runs same-origin so sso_remembered cookie is sent
  const ssoPublicUrl = (config.public.ssoPublicUrl as string) || 'http://accounts.local'
  const switchInitUrl = `${ssoPublicUrl}/switch-init?sub=${encodeURIComponent(sub)}&returnUrl=${encodeURIComponent(oidcReturnUrl)}`
  return sendRedirect(event, switchInitUrl, 302)
})
