/**
 * Browser-side OIDC Authorization Code + PKCE flow against Innovayse SSO.
 *
 * The admin panel is a pure Vite SPA with no server component, so — unlike
 * the client portal's Nuxt BFF — it cannot hold a client secret or an
 * httpOnly cookie for the PKCE verifier. It registers as a *public* OIDC
 * client instead (see hostpanel-admin in innovayse-sso's SsoSeeder) and runs
 * the whole code+PKCE exchange directly in the browser, which is the
 * standard pattern for SPA-only public clients.
 */

const SSO_URL = (import.meta.env.VITE_SSO_URL as string | undefined) ?? 'http://accounts.local'
const CLIENT_ID = (import.meta.env.VITE_SSO_CLIENT_ID as string | undefined) ?? 'hostpanel-admin'

const VERIFIER_KEY = 'admin_sso_pkce_verifier'
const STATE_KEY = 'admin_sso_state'

function base64UrlEncode(bytes: Uint8Array): string {
  let str = ''
  for (const b of bytes) str += String.fromCharCode(b)
  return btoa(str).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

async function sha256Base64Url(input: string): Promise<string> {
  const data = new TextEncoder().encode(input)
  const digest = await crypto.subtle.digest('SHA-256', data)
  return base64UrlEncode(new Uint8Array(digest))
}

function randomToken(byteLength: number): string {
  const bytes = crypto.getRandomValues(new Uint8Array(byteLength))
  return base64UrlEncode(bytes)
}

/** Builds the redirect_uri used for both the authorize request and the token exchange. */
function callbackUrl(): string {
  return `${window.location.origin}/auth/callback`
}

/**
 * Starts the login flow: generates a fresh PKCE pair + state, stashes the
 * verifier in sessionStorage (survives the redirect, cleared on tab close),
 * and navigates the browser to the SSO authorize endpoint.
 */
export async function startSsoLogin(): Promise<void> {
  const verifier = randomToken(32)
  const state = randomToken(16)

  // crypto.subtle is only available in secure contexts (HTTPS / localhost).
  // Fall back to PKCE method=plain when running over plain HTTP in dev.
  const hasSubtle = typeof crypto !== 'undefined' && !!crypto.subtle
  const challenge = hasSubtle ? await sha256Base64Url(verifier) : verifier
  const method = hasSubtle ? 'S256' : 'plain'

  sessionStorage.setItem(VERIFIER_KEY, verifier)
  sessionStorage.setItem(STATE_KEY, state)

  const params = new URLSearchParams({
    client_id: CLIENT_ID,
    response_type: 'code',
    redirect_uri: callbackUrl(),
    scope: 'openid profile email offline_access',
    code_challenge: challenge,
    code_challenge_method: method,
    state,
  })

  window.location.href = `${SSO_URL}/connect/authorize?${params}`
}

export interface SsoTokenResponse {
  access_token: string
  refresh_token?: string
  expires_in: number
  token_type: string
}

/**
 * Completes the flow after the SSO redirect back to /auth/callback: validates
 * `state`, exchanges `code` + the stashed verifier for tokens directly
 * against the SSO token endpoint (no secret needed — public client).
 *
 * @param query - The callback URL's query params (code, state, error).
 * @throws Error if state doesn't match, the verifier is missing, or the token exchange fails.
 */
export async function completeSsoLogin(query: URLSearchParams): Promise<SsoTokenResponse> {
  const error = query.get('error')
  if (error) throw new Error(query.get('error_description') ?? error)

  const code = query.get('code')
  const returnedState = query.get('state')
  const expectedState = sessionStorage.getItem(STATE_KEY)
  const verifier = sessionStorage.getItem(VERIFIER_KEY)

  sessionStorage.removeItem(STATE_KEY)
  sessionStorage.removeItem(VERIFIER_KEY)

  if (!code) throw new Error('Missing authorization code.')
  if (!expectedState || returnedState !== expectedState) throw new Error('Invalid OAuth state.')
  if (!verifier) throw new Error('Missing PKCE verifier — login likely started in another tab.')

  const body = new URLSearchParams({
    grant_type: 'authorization_code',
    client_id: CLIENT_ID,
    code,
    redirect_uri: callbackUrl(),
    code_verifier: verifier,
  })

  // Use a relative URL so the request goes through the Vite dev-server proxy
  // (/connect → SSO API internally). This avoids cross-origin POST issues that
  // arise when the browser hits accounts.local directly over plain HTTP.
  const res = await fetch(`/connect/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body,
  })

  if (!res.ok) {
    const text = await res.text().catch(() => '')
    throw new Error(`SSO token exchange failed: ${res.status} ${text}`)
  }

  return res.json() as Promise<SsoTokenResponse>
}

/**
 * Exchanges a refresh token for a new access token. Used for silent renewal
 * when the stored access token is close to expiry.
 *
 * @param refreshToken - The refresh token previously issued alongside the access token.
 */
export async function refreshSsoToken(refreshToken: string): Promise<SsoTokenResponse> {
  const body = new URLSearchParams({
    grant_type: 'refresh_token',
    client_id: CLIENT_ID,
    refresh_token: refreshToken,
  })

  const res = await fetch(`/connect/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body,
  })

  if (!res.ok) throw new Error(`SSO token refresh failed: ${res.status}`)
  return res.json() as Promise<SsoTokenResponse>
}

/** Redirects the browser to SSO's end-session endpoint, ending the SSO session too. */
export function ssoLogoutRedirect(idTokenHint?: string): void {
  const params = new URLSearchParams({ post_logout_redirect_uri: window.location.origin })
  if (idTokenHint) params.set('id_token_hint', idTokenHint)
  window.location.href = `${SSO_URL}/connect/logout?${params}`
}
