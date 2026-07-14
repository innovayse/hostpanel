/**
 * GET /api/portal/auth/sso/callback?code=...
 *
 * Exchanges the authorization code for tokens, stores them in httpOnly cookies,
 * then redirects to the client dashboard.
 */
export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig()
  const query = getQuery(event)

  // Silent SSO (prompt=none) returned an error — not logged in, go back to homepage
  const error = query.error as string | undefined
  if (error) {
    deleteCookie(event, 'pkce_verifier', { path: '/' })
    deleteCookie(event, 'oauth_state', { path: '/' })
    deleteCookie(event, 'sso_silent_tried', { path: '/' })
    const returnTo = getCookie(event, 'sso_silent_return') || '/'
    deleteCookie(event, 'sso_silent_return', { path: '/' })
    return sendRedirect(event, returnTo, 302)
  }

  const code = query.code as string | undefined
  if (!code) {
    throw createError({ statusCode: 400, statusMessage: 'Missing authorization code' })
  }

  // Validate state to prevent CSRF
  const storedState = getCookie(event, 'oauth_state')
  const returnedState = query.state as string | undefined
  if (!storedState || storedState !== returnedState) {
    throw createError({ statusCode: 400, statusMessage: 'Invalid state parameter' })
  }
  deleteCookie(event, 'oauth_state', { path: '/' })

  const codeVerifier = getCookie(event, 'pkce_verifier')
  if (!codeVerifier) {
    throw createError({ statusCode: 400, statusMessage: 'Missing PKCE verifier — session expired' })
  }

  // Clear the PKCE cookie immediately
  deleteCookie(event, 'pkce_verifier', { path: '/' })

  // Exchange code for tokens (server-to-server, uses internal SSO URL)
  let tokenResponse: {
    access_token: string
    refresh_token?: string
    expires_in: number
    token_type: string
  }

  try {
    tokenResponse = await $fetch(`${config.ssoUrl}/connect/token`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({
        grant_type: 'authorization_code',
        code,
        redirect_uri: config.ssoCallbackUrl,
        client_id: config.ssoClientId,
        client_secret: config.ssoClientSecret,
        code_verifier: codeVerifier,
      }).toString(),
    })
  } catch (err: unknown) {
    const e = err as { data?: { error?: string }; statusCode?: number }
    throw createError({
      statusCode: 401,
      statusMessage: e?.data?.error ?? 'Token exchange failed',
    })
  }

  const { access_token, refresh_token, expires_in } = tokenResponse

  // Store access token in httpOnly cookie
  setCookie(event, 'auth_token', access_token, {
    httpOnly: true,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'lax',
    maxAge: expires_in ?? 60 * 15,
    path: '/',
  })

  // Store refresh token if provided
  if (refresh_token) {
    setCookie(event, 'refresh_token', refresh_token, {
      httpOnly: true,
      secure: process.env.NODE_ENV === 'production',
      sameSite: 'lax',
      maxAge: 60 * 60 * 24 * 7,
      path: '/',
    })
  }

  // Non-httpOnly flag for client-side middleware
  setCookie(event, 'authed', '1', {
    httpOnly: false,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'lax',
    maxAge: 60 * 60 * 24 * 7,
    path: '/',
  })

  // If came from silent SSO on a public page — return there (now logged in)
  deleteCookie(event, 'sso_silent_tried', { path: '/' })
  const silentReturn = getCookie(event, 'sso_silent_return')
  deleteCookie(event, 'sso_silent_return', { path: '/' })

  return sendRedirect(event, silentReturn || '/client/dashboard', 302)
})
