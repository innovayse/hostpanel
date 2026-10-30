/**
 * GET /api/portal/auth/sso/callback?code=<code>&state=<state>
 *
 * Exchanges the authorization code for tokens, keeps them in the server-side session store (the
 * browser receives only the opaque session cookie), then redirects back.
 *
 * The attempt is found through the returned `state` alone: the sealed transaction cookie named after
 * it (`server/utils/signon-transaction.ts`) holds the PKCE verifier, the page to return to and the
 * kind of attempt. A callback whose `state` has no matching, authentic, live transaction proves
 * nothing, so it is never accepted and nothing is deleted for it: another attempt of this browser
 * that is still in progress is left alone. That is what keeps a stray or forged callback from
 * ending someone else's sign-in, and a callback that this browser did not start from being accepted.
 *
 * Once a transaction is found it is deleted (single use) before anything else is decided. When the
 * identity provider answers with an error instead of a code (a silent `prompt=none` probe that found
 * no session answers `login_required`), when the code is missing and when the code exchange fails,
 * the visitor is returned to the page they were on with the sign-in prompt flag
 * (`?signin=required`), so the page says they are not signed in and offers the sign-in. A callback
 * never answers with a JSON body, and never repeats what the provider said.
 *
 * Every outcome is logged once with a fixed reason code (`SignonFailureReason`); the code, the
 * state, the tokens and the provider's reply are never logged. The response is `no-store` and sends
 * no `Referer`, because its address carries the one-time code.
 *
 * @module server/api/portal/auth/sso/callback.get
 */

import type { H3Event } from 'h3'
import { CLIENT_DASHBOARD_PATH } from '~/constants/clientRoutes'
import { SessionCookie } from '~/constants/sessionCookie'
import { cookieOptions } from '~/server/utils/auth-cookie'
import { startSession } from '~/server/utils/portal-session'
import type { SessionTokens } from '~/server/utils/portal-session'
import { readQueryString } from '~/server/utils/readQueryString'
import { requireSsoSetting } from '~/server/utils/requireSsoSetting'
import { safeReturnPath } from '~/server/utils/signon-flow'
import { deleteTransaction, openTransaction } from '~/server/utils/signon-transaction'
import { HttpMethod } from '~/types/httpmethod'
import { SignonFailureReason } from '~/types/signonfailurereason'
import { SignonTransactionStatus } from '~/types/signontransactionstatus'
import { withSignInPrompt } from '~/utils/returnPath'

/** The SSO token endpoint's reply (OAuth field names, as the standard spells them). */
interface SsoTokenResponse {
  /** Access token for the portal session. */
  access_token: string
  /** Refresh token, when the SSO issued one. */
  refresh_token?: string
  /** Lifetime of the access token, in seconds. */
  expires_in: number
  /** Token type, always `Bearer`. */
  token_type: string
}

/** The page the visitor lands on when the attempt tells nowhere else. */
const HOME_PATH = '/'

/** The cookies of the old shared-cookie flow, cleared once a valid transaction is consumed. */
const LEGACY_FLOW_COOKIES = [
  SessionCookie.LegacyOauthState,
  SessionCookie.LegacyPkceVerifier,
  SessionCookie.LegacyPostLoginRedirect,
  SessionCookie.LegacySsoSilentReturn
]

/**
 * Logs why a callback produced no session. Only the fixed reason is written.
 *
 * @param reason - Why the attempt ended.
 */
const logEnded = (reason: SignonFailureReason): void => {
  if (reason === SignonFailureReason.TamperedTransaction) {
    console.warn(`[sso] sign-in callback refused: ${reason}`)
    return
  }
  console.info(`[sso] sign-in attempt ended: ${reason}`)
}

/**
 * Maps a failed transaction lookup to the reason that is logged.
 *
 * @param status - The lookup status, never `Found`.
 * @returns The reason code.
 */
const reasonForLookup = (status: SignonTransactionStatus): SignonFailureReason => {
  switch (status) {
    case SignonTransactionStatus.Expired:
      return SignonFailureReason.Expired
    case SignonTransactionStatus.Tampered:
      return SignonFailureReason.TamperedTransaction
    default:
      return SignonFailureReason.UnknownState
  }
}

/**
 * Ends a sign-in attempt that will not produce a session and sends the visitor back to the page
 * they were on, flagged so it shows the sign-in prompt. A browser navigation must land on a page,
 * never on a JSON error body.
 *
 * `sso_silent_tried` is deliberately left in place: it is the loop guard, and clearing it here would
 * send the very next page render straight back through /authorize. It carries a short TTL of its
 * own, so a later visit still gets a fresh session probe.
 *
 * @param event - The callback request.
 * @param reason - Why the attempt ended, for the log.
 * @param returnTo - The page the attempt remembered, or null when none is known.
 * @returns The redirect to the page the visitor came from, with the sign-in prompt flag.
 */
const endSignInAttempt = (event: H3Event, reason: SignonFailureReason, returnTo: string | null) => {
  logEnded(reason)
  return sendRedirect(event, withSignInPrompt(safeReturnPath(returnTo) ?? HOME_PATH), 302)
}

export default defineEventHandler(async (event) => {
  // The address carries a one-time code: never cache the answer, never leak the address as a referrer.
  setResponseHeaders(event, { 'Referrer-Policy': 'no-referrer', 'Cache-Control': 'no-store' })

  const config = useRuntimeConfig()
  const query = getQuery(event)
  // A repeated key is refused (400) rather than read as whichever value comes first.
  const error = readQueryString(query.error)
  const code = readQueryString(query.code)
  const returnedState = readQueryString(query.state)
  // Fail closed: the token exchange sends the callback address as `redirect_uri`, so an empty one can never succeed.
  const redirectUri = requireSsoSetting(config.ssoCallbackUrl)

  // The only way to an attempt is the returned state. Without a matching transaction the request
  // proves nothing (a stale address, a forged one, an attempt that outlived its cookie): it is not
  // accepted and, since it proves nothing, it deletes nothing either.
  const lookup = openTransaction(event, returnedState)
  if (lookup.status !== SignonTransactionStatus.Found) {
    return endSignInAttempt(event, reasonForLookup(lookup.status), null)
  }
  const { transaction } = lookup

  // Single use: gone before the outcome is decided, so a replay of this address finds nothing.
  deleteTransaction(event, transaction.state)
  for (const legacy of LEGACY_FLOW_COOKIES) {
    deleteCookie(event, legacy, cookieOptions(0))
  }

  // The provider answered with an error (a silent probe with no session answers `login_required`).
  if (error) {
    return endSignInAttempt(event, SignonFailureReason.ProviderError, transaction.returnTo)
  }

  // A callback with no code is not a sign-in the provider finished.
  if (!code) {
    return endSignInAttempt(event, SignonFailureReason.MissingCode, transaction.returnTo)
  }

  // Exchange code for tokens (server-to-server, uses internal SSO URL)
  let tokenResponse: SsoTokenResponse

  // Bound to a plain `string` and given an explicit response type. Handed a template literal
  // with no generic, Nitro tries to infer the reply by matching this URL against its own route
  // table and recurses until TypeScript gives up with TS2321: for the SSO's token endpoint,
  // which matches no Nitro route. The shape is the one declared above.
  const tokenUrl: string = `${config.ssoUrl}/connect/token`

  try {
    tokenResponse = await $fetch<typeof tokenResponse>(tokenUrl, {
      method: HttpMethod.Post,
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({
        grant_type: 'authorization_code',
        code,
        redirect_uri: redirectUri,
        client_id: config.ssoClientId,
        client_secret: config.ssoClientSecret,
        code_verifier: transaction.verifier,
      }).toString(),
    })
  } catch {
    // Neither the provider's error body nor the failure itself is kept: the reason code is all that is told.
    return endSignInAttempt(event, SignonFailureReason.ExchangeFailed, transaction.returnTo)
  }

  const { access_token, refresh_token, expires_in } = tokenResponse

  // The tokens stay on the server. A refresh token can be larger than the 4 KB a browser keeps per
  // cookie, and written as one the browser would drop it and end the session with the access
  // token.
  const tokens: SessionTokens = { accessToken: access_token }
  if (refresh_token) {
    tokens.refreshToken = refresh_token
  }
  if (expires_in !== undefined) {
    tokens.expiresIn = expires_in
  }
  await startSession(event, tokens)

  // No readable identity cookie is written for the header widget: it asks the sign-on
  // provider directly, through the /accounts-embed iframe, and the provider's own remembered
  // list already includes this account by the time it redirects back here. Such a cookie
  // would publish the user's identity to every script on this origin, and to anything
  // logging request headers.

  // A silent probe on a public page ends here too: the guard is no longer needed once signed in.
  deleteCookie(event, SessionCookie.SsoSilentTried, cookieOptions(0))

  // The page the attempt started on (e.g. "Add another account" should bring back to the current
  // page, not the dashboard). It was checked when sealed and is checked again before it is followed.
  return sendRedirect(event, safeReturnPath(transaction.returnTo) ?? CLIENT_DASHBOARD_PATH, 302)
})
