import { randomBytes, createHash } from 'node:crypto'

/**
 * GET /api/portal/auth/sso/authorize
 *
 * Generates a PKCE pair, stores code_verifier in a short-lived httpOnly cookie,
 * then redirects the browser to the SSO authorization endpoint.
 */
export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig()

  // Generate PKCE
  const codeVerifier = randomBytes(32).toString('base64url')
  const codeChallenge = createHash('sha256')
    .update(codeVerifier)
    .digest('base64url')

  // Store verifier server-side in an httpOnly cookie (5 min TTL)
  setCookie(event, 'pkce_verifier', codeVerifier, {
    httpOnly: true,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'lax',
    maxAge: 60 * 5,
    path: '/',
  })

  // Generate and store state for CSRF protection
  const state = randomBytes(16).toString('hex')
  setCookie(event, 'oauth_state', state, {
    httpOnly: true,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'lax',
    maxAge: 60 * 5,
    path: '/',
  })

  const redirectUri = config.ssoCallbackUrl
  const params = new URLSearchParams({
    client_id: config.ssoClientId,
    response_type: 'code',
    redirect_uri: redirectUri,
    scope: 'openid profile email offline_access',
    code_challenge: codeChallenge,
    code_challenge_method: 'S256',
    state,
  })

  const authorizeUrl = `${config.public.ssoPublicUrl}/connect/authorize?${params}`
  return sendRedirect(event, authorizeUrl, 302)
})
