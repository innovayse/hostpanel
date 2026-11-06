/**
 * The start of the external sign-on authorization-code flow, shared by every route that sends the
 * browser to the sign-on provider (`authorize.get.ts`, `switch.get.ts`).
 *
 * One place creates the PKCE pair, the `state` and the `nonce`, binds them to this browser in one sealed,
 * short-lived httpOnly transaction cookie of their own (`server/utils/signon-transaction.ts`), and
 * builds the authorize query. The callback later finds the transaction through the returned
 * `state` and checks the PKCE proof against it, so a route that only starts the flow never changes
 * the local session, and attempts started side by side never overwrite one another.
 *
 * @module server/utils/signon-flow
 */

import { randomBytes, createHash } from 'node:crypto'
import type { H3Event } from 'h3'
import { requireSsoSetting } from '~/server/utils/requireSsoSetting'
import { createTransaction } from '~/server/utils/signon-transaction'
import { SignonTransactionKind } from '~/types/signontransactionkind'
import { isInternalPath } from '~/utils/returnPath'

/** The scopes the flow requests. */
const SIGNON_SCOPE = 'openid profile email offline_access'

/** The longest `login_hint` or account id passed on; the longest valid e-mail address is 254 characters. */
export const MAX_LOGIN_HINT_LENGTH = 254

/**
 * The longest return path kept, in UTF-8 bytes. The path is sealed into a cookie, and up to
 * `SSO_MAX_LIVE_TRANSACTIONS` such cookies are sent back with every request to the host, next to the
 * session cookies. Servers and proxies refuse a request header of about 8 KB with a 400, which would
 * lock the browser out of the whole host until the cookies expire. The limit is in bytes, not
 * characters, because the cookie grows with bytes: a path of multibyte characters is two to four
 * times its character count. 512 bytes keeps five attempts near 5 KB and every path the app builds
 * (a page address with a short query) far below it. A longer path is dropped, not an error: the
 * visitor lands on the default page after signing in.
 */
export const MAX_RETURN_PATH_BYTES = 512

/** A started flow: where the provider lives and the authorize query bound to this browser. */
export interface SignonFlow {
  /** The public address of the sign-on provider, never empty. */
  ssoPublicUrl: string
  /** The authorize query: client, redirect address, scope, PKCE challenge, `state` and `nonce`. */
  params: URLSearchParams
}

/**
 * Keeps a path only when it is safe to return the visitor to after sign-in. The one validator of the
 * whole flow: the routes that start it use it before the path is sealed, and the callback uses it
 * again before the path is followed.
 *
 * @param candidate - A requested return path, if any.
 * @returns The path when it is inside this app, not an `/api/` route and not larger than `MAX_RETURN_PATH_BYTES` in UTF-8, otherwise null.
 */
export const safeReturnPath = (candidate: unknown): string | null => {
  return isInternalPath(candidate) && !candidate.startsWith('/api/') && Buffer.byteLength(candidate, 'utf8') <= MAX_RETURN_PATH_BYTES
    ? candidate
    : null
}

/**
 * Starts the sign-on flow for this browser.
 *
 * Fails closed with a coded 503 before any cookie is written when the sign-on addresses are not
 * configured. Otherwise seals the PKCE verifier, the `state`, the `nonce`, the return path and the kind of attempt
 * into one transaction cookie, and returns the authorize query.
 *
 * @param event - The request being answered.
 * @param returnTo - A path already checked with `safeReturnPath`, or null to remember none.
 * @param kind - An interactive sign-in (the default) or a silent probe.
 * @returns The provider address and the authorize query.
 * @throws H3Error 503 carrying `data.code = SSO_NOT_CONFIGURED` when an address is missing.
 */
export const startSignonFlow = (
  event: H3Event,
  returnTo: string | null,
  kind: SignonTransactionKind = SignonTransactionKind.Interactive
): SignonFlow => {
  const config = useRuntimeConfig()
  const ssoPublicUrl = requireSsoSetting(config.public.ssoPublicUrl)
  const redirectUri = requireSsoSetting(config.ssoCallbackUrl)

  const codeVerifier = randomBytes(32).toString('base64url')
  const codeChallenge = createHash('sha256')
    .update(codeVerifier)
    .digest('base64url')

  const state = randomBytes(16).toString('hex')
  // Echoed back inside the signed ID token, so a token minted for another attempt is refused.
  const nonce = randomBytes(16).toString('hex')
  createTransaction(event, { state, verifier: codeVerifier, nonce, returnTo, kind })

  const params = new URLSearchParams({
    client_id: config.ssoClientId,
    response_type: 'code',
    redirect_uri: redirectUri,
    scope: SIGNON_SCOPE,
    code_challenge: codeChallenge,
    code_challenge_method: 'S256',
    state,
    nonce,
  })
  return { ssoPublicUrl, params }
}
