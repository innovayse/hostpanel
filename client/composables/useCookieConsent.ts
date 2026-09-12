/** What a visitor agreed to: every cookie, only the ones the site needs, or nothing yet. */
export type CookieConsent = 'all' | 'essential' | null

/** The cookie the banner writes. */
export const COOKIE_CONSENT_NAME = 'cookie-consent'

/** How long a choice is kept, in seconds: one year. */
const COOKIE_CONSENT_MAX_AGE = 60 * 60 * 24 * 365

/**
 * The visitor's cookie consent, readable anywhere and written only by the banner.
 *
 * The banner used to keep this in two private helpers and then do nothing with the
 * answer — "here you would disable analytics" stayed a comment. Now the tag-manager
 * loader (`plugins/tracking.client.ts`) watches {@link consent} and loads only on `all`.
 *
 * A cookie rather than local storage so the server can read it too; `useCookie` keeps the
 * ref in step with the browser's copy.
 *
 * @returns The current consent and a setter.
 */
export const useCookieConsent = () => {
  const cookie = useCookie<string | null>(COOKIE_CONSENT_NAME, {
    sameSite: 'lax',
    maxAge: COOKIE_CONSENT_MAX_AGE,
  })

  /** Narrowed: anything but the two known values reads as "no choice made". */
  const consent = computed<CookieConsent>(() =>
    cookie.value === 'all' || cookie.value === 'essential' ? cookie.value : null)

  /**
   * Records the visitor's choice.
   *
   * @param level What they agreed to.
   */
  function accept(level: Exclude<CookieConsent, null>): void {
    cookie.value = level
  }

  return { consent, accept }
}
