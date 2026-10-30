/**
 * Names of the cookies that carry the sign-in session between the browser and the Nitro routes.
 *
 * One enum so the routes that set a cookie, the ones that read it and the ones that clear it
 * cannot disagree about its name.
 *
 * @module constants/sessionCookie
 */

/** The session cookies the portal sets or reads. */
export enum SessionCookie {
  /**
   * The opaque id of the signed-in session; httpOnly. The tokens it names stay on the server
   * (`server/utils/portal-session.ts`), so this is the only credential the browser holds.
   */
  Session = 'portal_session',
  /** Legacy cookie that carried the raw access token; only ever cleared now. */
  LegacyAuthToken = 'auth_token',
  /**
   * Legacy cookie that carried the raw refresh token; only ever cleared now. A refresh token can be
   * larger than the 4 KB a browser keeps per cookie, so the browser dropped it and the session
   * ended with the access token.
   */
  LegacyRefreshToken = 'refresh_token',
  /** Readable flag (`1`) that tells the browser a session exists without exposing the token. */
  Authed = 'authed',
  /** Marks that the silent single sign-on attempt has been made for this visit. */
  SsoSilentTried = 'sso_silent_tried',
  /**
   * Legacy shared cookie of the sign-in round trip (the anti-forgery state); only ever cleared now.
   * Every attempt is a sealed transaction cookie of its own (`SSO_TRANSACTION_COOKIE_STEM`).
   */
  LegacyOauthState = 'oauth_state',
  /** Legacy shared cookie of the sign-in round trip (the PKCE verifier); only ever cleared now. */
  LegacyPkceVerifier = 'pkce_verifier',
  /** Legacy shared cookie of the sign-in round trip (the page to return to); only ever cleared now. */
  LegacyPostLoginRedirect = 'post_login_redirect',
  /** Legacy shared cookie of the silent probe (the page to return to); only ever cleared now. */
  LegacySsoSilentReturn = 'sso_silent_return',
  /** Legacy readable cookie that once carried the signed-in account to the widget; only ever cleared now. */
  PendingAccount = 'inno_pending_account'
}

/**
 * Name stem of a sign-in transaction cookie. The full name is
 * `<SSO_TRANSACTION_COOKIE_STEM><first SSO_TRANSACTION_NAME_HASH_LENGTH hex characters of sha256(state)>`,
 * preceded by `HOST_COOKIE_PREFIX` outside development.
 */
export const SSO_TRANSACTION_COOKIE_STEM = 'sso_tx_'

/** How many hex characters of the `state` digest follow the stem, so concurrent attempts get distinct cookies. */
export const SSO_TRANSACTION_NAME_HASH_LENGTH = 16

/**
 * Prefix that makes a browser accept a cookie only when it is Secure, has path `/` and no Domain
 * attribute. Not used in development, where the site runs over plain HTTP.
 */
export const HOST_COOKIE_PREFIX = '__Host-'
