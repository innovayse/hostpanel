/**
 * GET /api/portal/auth/sso/authorize
 *
 * Starts the sign-on flow (PKCE verifier, state and return path sealed into a transaction cookie of
 * their own, see `server/utils/signon-flow.ts`), then redirects the browser to the SSO authorization endpoint.
 *
 * An `authuser` query (the account a visitor arrived for, as the products of a shared sign-on add
 * to their links) is passed on as `login_hint`, so the identity provider picks or pre-fills that
 * account instead of whichever one it signed in last.
 *
 * `prompt=none` is the silent probe (`server/middleware/sso-silent.ts`); it is recorded as a silent
 * attempt, any other request as an interactive one.
 *
 * @module server/api/portal/auth/sso/authorize.get
 */

import { LOGIN_HINT_QUERY } from '~/constants/clientRoutes'
import { readQueryString } from '~/server/utils/readQueryString'
import { MAX_LOGIN_HINT_LENGTH, safeReturnPath, startSignonFlow } from '~/server/utils/signon-flow'
import { SignonTransactionKind } from '~/types/signontransactionkind'

/** The `prompt` value that asks the provider to answer without showing any page. */
const PROMPT_NONE = 'none'

export default defineEventHandler(async (event) => {
  // A repeated key is refused (400) before any cookie is set.
  const query = getQuery(event)
  const prompt = readQueryString(query.prompt)
  const loginHint = readQueryString(query[LOGIN_HINT_QUERY])
  const requestedReturnTo = readQueryString(query.returnTo)

  // Save current page so callback can return here instead of always going to dashboard
  let returnTo = requestedReturnTo || ''
  if (!returnTo) {
    const referer = getHeader(event, 'referer') || ''
    if (referer) {
      try { returnTo = new URL(referer).pathname } catch { /* ignore */ }
    }
  }

  // Fails closed with 503 before any cookie is set when the sign-on addresses are not configured.
  const kind = prompt === PROMPT_NONE ? SignonTransactionKind.Silent : SignonTransactionKind.Interactive
  const { ssoPublicUrl, params } = startSignonFlow(event, safeReturnPath(returnTo), kind)

  // If the user explicitly wants to switch accounts, pass prompt=select_account
  if (prompt) {
    params.set('prompt', prompt)
  }

  // The account the visitor arrived for. Capped: an address is never longer, and the value travels on in a URL.
  if (loginHint && loginHint.trim() && loginHint.length <= MAX_LOGIN_HINT_LENGTH) {
    params.set('login_hint', loginHint.trim())
  }

  return sendRedirect(event, `${ssoPublicUrl}/connect/authorize?${params}`, 302)
})
