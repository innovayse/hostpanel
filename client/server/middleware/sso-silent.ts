/**
 * Server middleware: silent SSO
 *
 * Runs on every request before the page renders.
 * If a person's browser opens a page with no `authed` cookie and we haven't tried silent SSO yet,
 * redirects to SSO with prompt=none (no login screen shown).
 *
 * Only a document navigation is probed: a GET (never HEAD) that the browser marks as a document
 * (`Sec-Fetch-Dest: document`) or that asks for HTML, from a user agent that is not a known bot,
 * preview fetcher, monitor or scripted client. Those keep no cookies, so the probe could never
 * finish for them: they would be sent round the redirect again and again. A page that already
 * carries the sign-in prompt flag (`?signin=required`) was returned by a probe that found no
 * session; it is not probed again, so even a browser that refuses cookies does not loop.
 *
 * SSO responds:
 *   - Active session  → issues code → callback sets authed → back to same page (logged in, no flash)
 *   - No session      → login_required error → callback returns to the page with the sign-in
 *                        prompt flag, so the page offers the sign-in instead of rendering as a silent guest
 *
 * The probe is skipped while this browser has an interactive sign-in under way (a person typing a
 * password in another tab): the page is shown as it is, and a later navigation probes as usual.
 *
 * An `authuser` query on the page (the account the visitor arrived for) is handed to the probe, which
 * passes it to the identity provider as `login_hint`.
 *
 * @module server/middleware/sso-silent
 */

import type { H3Event } from 'h3'
import {
  LOGIN_HINT_QUERY,
  SIGN_IN_PROMPT_QUERY,
  SIGN_IN_PROMPT_VALUE,
  SSO_AUTHORIZE_PATH
} from '~/constants/clientRoutes'
import { AuthMode } from '~/types/authmode'
import { HttpMethod } from '~/types/httpmethod'
import { SessionCookie } from '~/constants/sessionCookie'
import { SSO_SILENT_COOKIE_MAX_AGE, cookieOptions } from '~/server/utils/auth-cookie'
import { isBotUserAgent } from '~/server/utils/bot-agent'
import { hasPendingInteractiveTransaction } from '~/server/utils/signon-transaction'

/** The query key that carries the page to return to once the probe finishes. */
const RETURN_TO_QUERY = 'returnTo'

/** The value of `Sec-Fetch-Dest` for a navigation to a page. */
const FETCH_DEST_DOCUMENT = 'document'

/** The media type a page navigation asks for. */
const HTML_MEDIA_TYPE = 'text/html'

/**
 * Whether the request is a browser opening a page: a GET, from a user agent that is a browser,
 * that the browser marks as a document or that accepts HTML.
 *
 * @param event - The incoming request.
 * @returns True for a document navigation.
 */
const isDocumentNavigation = (event: H3Event): boolean => {
  if (event.method !== HttpMethod.Get || isBotUserAgent(getHeader(event, 'user-agent'))) {
    return false
  }
  const fetchDest = getHeader(event, 'sec-fetch-dest')
  if (fetchDest) {
    return fetchDest === FETCH_DEST_DOCUMENT
  }
  return (getHeader(event, 'accept') ?? '').includes(HTML_MEDIA_TYPE)
}

/** Sends a signed-out page navigation through one silent SSO probe. */
export default defineEventHandler((event) => {
  // Local-auth mode has its own login flow (client-auth.ts middleware), never applicable to SSO.
  const config = useRuntimeConfig(event)
  if (config.authMode !== AuthMode.Sso) {
    return
  }

  // Only intercept page navigations (not API calls, assets, etc.)
  const url = event.path ?? ''
  // The file test looks at the path only: an `?authuser=` address in the query has dots too.
  const pathname = url.split('?')[0] ?? ''
  if (
    url.startsWith('/api/') ||
    url.startsWith('/_nuxt/') ||
    url.startsWith('/__nuxt') ||
    pathname.includes('.')
  ) {
    return
  }

  if (!isDocumentNavigation(event)) {
    return
  }

  const authed = getCookie(event, SessionCookie.Authed)
  const silentTried = getCookie(event, SessionCookie.SsoSilentTried)

  if (authed || silentTried) {
    return
  }

  const query = getQuery(event)
  // The page a failed probe came back to: probing again would only repeat the answer.
  if (query[SIGN_IN_PROMPT_QUERY] === SIGN_IN_PROMPT_VALUE) {
    return
  }

  // A person is completing a sign-in elsewhere: starting a probe now would only compete with it.
  if (hasPendingInteractiveTransaction(event)) {
    return
  }

  // Mark that we've tried: prevents redirect loop when SSO returns login_required
  setCookie(event, SessionCookie.SsoSilentTried, '1', cookieOptions(SSO_SILENT_COOKIE_MAX_AGE))

  // The page to return to travels in the probe's address; the authorize route seals it into the attempt.
  const probe = new URLSearchParams({ prompt: 'none', [RETURN_TO_QUERY]: url || '/' })
  const loginHint = query[LOGIN_HINT_QUERY]
  if (typeof loginHint === 'string' && loginHint) {
    probe.set(LOGIN_HINT_QUERY, loginHint)
  }
  return sendRedirect(event, `${SSO_AUTHORIZE_PATH}?${probe}`, 302)
})
