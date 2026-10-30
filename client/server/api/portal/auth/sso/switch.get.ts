/**
 * GET /api/portal/auth/sso/switch?sub=XXX&redirect=/some/path
 *
 * Switches to a remembered account. The caller is a header widget that navigates the browser here,
 * so this is a GET that answers with a redirect.
 *
 * The browser is sent to the sign-on provider's `switch-init` page with the account (`sub`) and a
 * relative authorize address to continue to. That page runs on the provider's own origin, so the
 * provider's remembered-accounts cookie is sent with it; this app cannot read that cookie, which is
 * why the account is chosen there and not through a hint on the authorize request.
 *
 * Why a GET is safe: the handler never changes the local session. It only starts the sign-on
 * authorize flow, exactly as `authorize.get.ts` does: a fresh `state` and PKCE verifier are bound to
 * this browser in a sealed httpOnly transaction cookie. The account actually changes in `callback.get.ts`, after the
 * returned `state` and the PKCE proof are verified against that cookie. A cross-site GET can
 * therefore at most send the visitor through the sign-on page for an account they already have; it
 * cannot sign them in as anyone or end their session.
 *
 * A repeated `sub` or `redirect` key is a coded 400 before any cookie is written. Only a path inside
 * this app is kept as the place to return to; anything else falls back to the client area.
 *
 * @module server/api/portal/auth/sso/switch.get
 */

import { CLIENT_AREA_PATH, SSO_AUTHORIZE_PATH } from '~/constants/clientRoutes'
import { readQueryString } from '~/server/utils/readQueryString'
import { MAX_LOGIN_HINT_LENGTH, safeReturnPath, startSignonFlow } from '~/server/utils/signon-flow'

export default defineEventHandler((event) => {
  // A repeated key is refused (400) before any cookie is set.
  const query = getQuery(event)
  const sub = readQueryString(query.sub)
  const redirect = readQueryString(query.redirect)

  if (!sub || !sub.trim() || sub.length > MAX_LOGIN_HINT_LENGTH) {
    return sendRedirect(event, SSO_AUTHORIZE_PATH, 302)
  }

  const { ssoPublicUrl, params } = startSignonFlow(event, safeReturnPath(redirect) ?? CLIENT_AREA_PATH)

  // The provider's switch-init page runs same-origin there, so its remembered-accounts cookie is sent.
  const authorizeReturnUrl = `/connect/authorize?${params}`
  const switchInitUrl = `${ssoPublicUrl}/switch-init?sub=${encodeURIComponent(sub.trim())}&returnUrl=${encodeURIComponent(authorizeReturnUrl)}`
  return sendRedirect(event, switchInitUrl, 302)
})
