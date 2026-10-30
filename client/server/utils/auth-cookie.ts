/**
 * Lifetime of a session that holds a refresh token, in seconds: the lifetime the identity
 * provider grants a refresh token (thirty days).
 *
 * Defined once so every place that stores a refresh token agrees: a shorter local value throws
 * away a credential the provider would still accept and sends the user through a sign-in they do
 * not need. If the provider's lifetime changes, change it here and nowhere else.
 */
export const REFRESH_TOKEN_MAX_AGE = 60 * 60 * 24 * 30

/**
 * Decodes the payload of a JWT without verifying it.
 *
 * The payload is only decoded, never trusted for access: the token was issued to this server by
 * the API or the identity provider, and its claims are read solely to size a session and to know
 * whose session it is.
 *
 * @param token - A compact JWT.
 * @returns The claims, or null when the token is not a JWT.
 */
export const readTokenPayload = (token: string): Record<string, unknown> | null => {
  const payload = token.split('.')[1]
  if (!payload) {
    return null
  }
  try {
    const json = Buffer.from(payload.replace(/-/g, '+').replace(/_/g, '/'), 'base64').toString('utf8')
    const claims = JSON.parse(json) as unknown
    return claims !== null && typeof claims === 'object' ? (claims as Record<string, unknown>) : null
  } catch {
    return null
  }
}

/**
 * Reads the `exp` claim (seconds since the epoch) of a JWT without verifying it.
 *
 * @param token - A compact JWT.
 * @returns The `exp` claim, or null when the token is not a JWT or carries no numeric `exp`.
 */
export const readTokenExpiry = (token: string): number | null => {
  const exp = readTokenPayload(token)?.['exp']
  return typeof exp === 'number' && Number.isFinite(exp) ? exp : null
}

/**
 * Cookie lifetime for an access token, taken from the token's own `exp` claim so the cookie
 * follows the API's configured session lifetime instead of a hard-coded number.
 *
 * @param token - The access token the API issued.
 * @param nowSeconds - Current time in seconds since the epoch; defaults to the clock.
 * @returns Seconds until `exp` (never negative), or undefined, a session cookie, when `exp` is missing.
 */
export const authCookieMaxAge = (token: string, nowSeconds: number = Math.floor(Date.now() / 1000)): number | undefined => {
  const exp = readTokenExpiry(token)
  if (exp === null) {
    return undefined
  }
  return Math.max(0, exp - nowSeconds)
}

/** Lifetime of the readable `authed` flag cookie, in seconds (seven days). */
export const AUTHED_FLAG_MAX_AGE = 60 * 60 * 24 * 7

/**
 * Lifetime, in seconds, of the cookie that carries a single sign-in round trip (the sealed
 * transaction holding the PKCE verifier, the state and the return address): fifteen minutes.
 *
 * The clock runs while a person is on the identity provider's page: choosing between remembered
 * accounts, typing a password, using a password manager, answering a second factor, retyping after
 * a mistake. That is minutes of ordinary human time, so a budget sized for the network round trip
 * (five minutes) sat too close to it. It bounds one attempt only: the state is single use and the
 * first callback clears it, so a longer value widens nothing.
 */
export const SSO_FLOW_COOKIE_MAX_AGE = 60 * 15

/**
 * The most sign-in attempts a browser keeps live at once (one sealed transaction cookie each).
 * Starting a sixth drops the oldest, so the cookies a browser sends back stay bounded.
 */
export const SSO_MAX_LIVE_TRANSACTIONS = 5

/** Lifetime, in seconds, of the loop guard of the silent sign-in attempt. */
export const SSO_SILENT_COOKIE_MAX_AGE = 30

/** Access-token lifetime, in seconds, assumed when the identity provider does not state one (15 minutes). */
export const ACCESS_TOKEN_FALLBACK_MAX_AGE = 60 * 15

/** The attributes every cookie this app writes carries. */
export interface PortalCookieOptions {
  /** Lifetime in seconds. */
  maxAge: number
  /** Hidden from page scripts. */
  httpOnly: boolean
  /** Sent over HTTPS only. */
  secure: boolean
  /** Cross-site policy; Lax, because the sign-in callback is a top-level redirect that Strict would not let carry the cookie. */
  sameSite: 'lax'
  /** Applies to the whole site. */
  path: string
}

/**
 * The options of every cookie a server route or middleware writes: httpOnly, Secure everywhere but
 * development, SameSite=Lax, path `/`. One definition, so no writer drifts from the others.
 *
 * @param maxAge - Lifetime in seconds (0 for a cookie being cleared).
 * @param httpOnly - False only for the readable `authed` hint the route middleware checks.
 * @returns The cookie options.
 */
export const cookieOptions = (maxAge: number, httpOnly: boolean = true): PortalCookieOptions => ({
  maxAge,
  httpOnly,
  secure: process.env.NODE_ENV !== 'development',
  sameSite: 'lax',
  path: '/',
})
