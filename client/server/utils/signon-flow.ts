/**
 * The start of the external sign-on authorization-code flow, shared by every route that sends the
 * browser to the sign-on provider (`authorize.get.ts`, `switch.get.ts`).
 *
 * One place creates the PKCE pair and the `state`, binds them to this browser in one sealed,
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
 * The longest return path kept. The path is sealed into a cookie, and a browser keeps about 4 KB
 * per cookie, so an unbounded one could make the whole transaction be dropped.
 */
export const MAX_RETURN_PATH_LENGTH = 1024

/** A started flow: where the provider lives and the authorize query bound to this browser. */
export interface SignonFlow {
  /** The public address of the sign-on provider, never empty. */
  ssoPublicUrl: string
  /** The authorize query: client, redirect address, scope, PKCE challenge and `state`. */
  params: URLSearchParams
}

/**
 * Keeps a path only when it is safe to return the visitor to after sign-in. The one validator of the
 * whole flow: the routes that start it use it before the path is sealed, and the callback uses it
 * again before the path is followed.
 *
 * @param candidate - A requested return path, if any.
 * @returns The path when it is inside this app, not an `/api/` route and not oversized, otherwise null.
 */
export const safeReturnPath = (candidate: unknown): string | null => {
  return isInternalPath(candidate) && !candidate.startsWith('/api/') && candidate.length <= MAX_RETURN_PATH_LENGTH
    ? candidate
    : null
}

/**
 * Starts the sign-on flow for this browser.
 *
 * Fails closed with a coded 503 before any cookie is written when the sign-on addresses are not
 * configured. Otherwise seals the PKCE verifier, the `state`, the return path and the kind of attempt
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
  createTransaction(event, { state, verifier: codeVerifier, returnTo, kind })

  const params = new URLSearchParams({
    client_id: config.ssoClientId,
    response_type: 'code',
    redirect_uri: redirectUri,
    scope: SIGNON_SCOPE,
    code_challenge: codeChallenge,
    code_challenge_method: 'S256',
    state,
  })
  return { ssoPublicUrl, params }
}
