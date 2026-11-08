/**
 * The "come back here after signing in" path: building the sign-in URL that carries it and
 * validating one that arrived in a query string.
 *
 * The value of `?redirect=` is attacker-controlled (anyone can send a customer a link), so it
 * is only ever followed when it is a path inside this app. An absolute URL, a protocol-relative
 * one (`//host`) or a backslash form browsers treat as one would turn the sign-in page into an
 * open redirect.
 *
 * @module utils/returnPath
 */

import { APP_LOCALES, DEFAULT_APP_LOCALE } from '~/constants/appLocales'
import {
  CLIENT_AREA_PATH,
  CLIENT_DASHBOARD_PATH,
  CLIENT_LOGIN_PATH,
  CLIENT_PUBLIC_ROUTES,
  CLIENT_REDIRECT_QUERY,
  LOGIN_HINT_QUERY,
  RETURN_TO_QUERY,
  SIGN_IN_PROMPT_QUERY,
  SIGN_IN_PROMPT_VALUE,
  SSO_AUTHORIZE_PATH
} from '~/constants/clientRoutes'

/**
 * Whether a string holds a control character, which has no business in a path and can smuggle a
 * different target. Compared by code point rather than by a regular expression, which the
 * `no-control-regex` rule rightly refuses.
 *
 * @param value - The string to scan.
 * @returns True when any character is below U+0020 or is U+007F.
 */
const hasControlCharacter = (value: string): boolean =>
  Array.from(value).some((character) => {
    const code = character.charCodeAt(0)
    return code < 0x20 || code === 0x7f
  })

/**
 * Removes the language prefix the router puts in front of every non-default locale
 * (`/ru/client/login` -> `/client/login`). The default language is served unprefixed, so its
 * paths pass through untouched.
 *
 * @param pathname - A path without query or hash.
 * @returns The path as the page file names it.
 */
const withoutLocalePrefix = (pathname: string): string => {
  const prefix = APP_LOCALES.find(
    locale => locale !== DEFAULT_APP_LOCALE && (pathname === `/${locale}` || pathname.startsWith(`/${locale}/`))
  )
  return prefix ? pathname.slice(prefix.length + 1) || '/' : pathname
}

/**
 * Whether a path is one of the pages that need no session (sign-in, register and the like).
 *
 * A localized address (`/ru/client/login`) is the same page as its unprefixed twin; judging the
 * prefixed form as private sent every Russian or Armenian visitor of the sign-in page back to the
 * English one.
 *
 * @param path - The path or full path to test; a query string or hash and a language prefix are ignored.
 * @returns True for a public page.
 */
export const isPublicClientPath = (path: string): boolean => {
  const pathname = withoutLocalePrefix(path.split(/[?#]/)[0] ?? '')
  return CLIENT_PUBLIC_ROUTES.some((route) => pathname === route || pathname.startsWith(`${route}/`))
}

/**
 * Whether a path belongs to the signed-in area: under `/client` and not one of the public pages
 * there (sign-in, register and the like). Everything else (the marketing pages, the catalogue, the cart)
 * is open to a visitor with no session, so an expired one must not move them off it.
 *
 * @param path - The path or full path to test; a query string or hash and a language prefix are ignored.
 * @returns True for a page that needs a session.
 */
export const isPrivateClientPath = (path: string): boolean => {
  const pathname = withoutLocalePrefix(path.split(/[?#]/)[0] ?? '')
  return (pathname === CLIENT_AREA_PATH || pathname.startsWith(`${CLIENT_AREA_PATH}/`)) && !isPublicClientPath(path)
}

/**
 * Whether a value is a path inside this app: it starts with a single `/`, carries no scheme,
 * host, backslash or control character.
 *
 * @param value - The candidate, typically a query-string value.
 * @returns True when it is safe to navigate to.
 */
export const isInternalPath = (value: unknown): value is string =>
  typeof value === 'string'
  && value.startsWith('/')
  && !value.startsWith('//')
  && !value.includes('\\')
  && !hasControlCharacter(value)

/**
 * Chooses where to go after signing in.
 *
 * @param value - The `redirect` query value as it arrived (a string, an array when repeated, or absent).
 * @param fallback - Where to go when the value is missing or unsafe.
 * @returns The requested path when it is internal and not itself a sign-in or register page
 * (which would loop), otherwise the fallback.
 */
export const safeReturnPath = (value: unknown, fallback: string = CLIENT_DASHBOARD_PATH): string =>
  isInternalPath(value) && !isPublicClientPath(value) ? value : fallback

/**
 * Builds the sign-in URL that returns to a page afterwards.
 *
 * @param fullPath - The page to return to, with its query and hash.
 * @returns `/client/login?redirect=<path>`, or the bare sign-in path when the page is itself public or not internal.
 */
export const loginPathWithReturn = (fullPath: string): string =>
  isInternalPath(fullPath) && !isPublicClientPath(fullPath)
    ? `${CLIENT_LOGIN_PATH}?${CLIENT_REDIRECT_QUERY}=${encodeURIComponent(fullPath)}`
    : CLIENT_LOGIN_PATH

/** Origin used only to parse a relative path with `URL`; it never appears in a result. */
const PARSE_ORIGIN = 'http://internal.invalid'

/**
 * Adds the sign-in prompt flag to a path, keeping its other query parameters and its hash.
 *
 * @param path - An internal path, with or without a query string.
 * @returns The same path with `signin=required` set.
 */
export const withSignInPrompt = (path: string): string => {
  const url = new URL(path, PARSE_ORIGIN)
  url.searchParams.set(SIGN_IN_PROMPT_QUERY, SIGN_IN_PROMPT_VALUE)
  return `${url.pathname}${url.search}${url.hash}`
}

/**
 * Removes the sign-in prompt flag from a path, keeping everything else.
 *
 * @param path - An internal path, with or without a query string.
 * @returns The path without `signin`.
 */
export const withoutSignInPrompt = (path: string): string => {
  const url = new URL(path, PARSE_ORIGIN)
  url.searchParams.delete(SIGN_IN_PROMPT_QUERY)
  return `${url.pathname}${url.search}${url.hash}`
}

/**
 * Builds the address that starts an interactive sign-in and comes back to a page afterwards.
 *
 * @param returnTo - The page to come back to; anything that is not an internal path is dropped.
 * @param loginHint - The account the visitor arrived for (`?authuser=`), passed on as `login_hint`;
 *   empty for none.
 * @returns The authorize address with its query.
 */
export const signInPath = (returnTo: string, loginHint: string = ''): string => {
  const params = new URLSearchParams()
  if (isInternalPath(returnTo)) {
    params.set(RETURN_TO_QUERY, withoutSignInPrompt(returnTo))
  }
  if (loginHint) {
    params.set(LOGIN_HINT_QUERY, loginHint)
  }
  const query = params.toString()
  return query ? `${SSO_AUTHORIZE_PATH}?${query}` : SSO_AUTHORIZE_PATH
}
