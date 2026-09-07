/**
 * Wire shape for `GET /api/settings/public`.
 *
 * The endpoint is `[AllowAnonymous]` and returns an allow-listed subset of the settings
 * table, which is what lets the login screen — rendered with no session — show the
 * operator's own logo rather than this product's.
 */

/** One row of `GET /api/settings/public`. */
export interface PublicSetting {
  /** Setting key, e.g. `portal.logo`. */
  key: string
  /** Its value. The endpoint omits rows whose value is empty. */
  value: string
}
