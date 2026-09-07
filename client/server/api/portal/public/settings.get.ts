/**
 * GET /api/portal/public/settings
 *
 * Returns the `portal.*` settings the storefront needs — the active template, the
 * operator-uploaded branding (logo, favicon) and the contact/newsletter widget
 * configuration.
 *
 * The key list below has to stay in step with `GetPublicSettingsHandler`'s own
 * allow-list on the backend. A key the backend serves but this list omits is
 * dropped here without a trace, and the storefront renders its build-time default
 * as though the operator had never set the value.
 *
 * **Deliberately uncached.** This was a `defineCachedEventHandler` with `maxAge: 60` and
 * `swr: true`, and that combination is what made the admin panel appear broken: an operator
 * uploaded a logo, reloaded the storefront, saw the old one, and had no way to tell whether
 * the setting had saved. Sixty seconds is bad enough; `swr` makes it worse, because the first
 * request after expiry is still answered from the stale entry while the refresh happens behind
 * it -- so the wait is a minute *and then* one more page load.
 *
 * What it bought was one indexed read of a table with a few dozen rows, on a request that is
 * already talking to the same API for the page it is rendering. That is not a trade worth a
 * minute of stale branding. The response still transfers to the browser with the SSR payload,
 * so a visitor makes this call once per page render, not once per component that reads a
 * setting.
 *
 * Two things this deliberately does not do. It never returns the whole settings
 * table: that table holds integration credentials, and this endpoint is
 * unauthenticated. And it never fails the request — a backend that is down, or
 * an older backend with no public settings endpoint at all, yields an empty
 * object so the caller falls back to its environment defaults rather than the
 * site failing to render.
 */

/** The only keys this endpoint will ever expose. */
const PUBLIC_KEYS = [
  'portal.template',
  'portal.logo',
  'portal.favicon',
  'portal.contact.whatsapp',
  'portal.contact.telegram',
  'portal.chat.provider',
  'portal.newsletter.action_url',
  'portal.contact.email',
  'portal.social.facebook',
  'portal.social.instagram',
  'portal.social.linkedin',
  'portal.social.youtube',
  'portal.contact.phone',
  'portal.legal.tax_id',
  'portal.apps.enabled',
  'portal.apps.account',
  'portal.apps.tasks',
  'portal.apps.erp',
  'portal.apps.hostpanel',
  'portal.apps.sheets',
  'portal.apps.mail',
  'portal.apps.docs',
  'portal.apps.calendar',
] as const

export default defineEventHandler(async (event) => {
  try {
    const rows = await internalApiCall<{ key: string, value: string }[]>(event, '/settings/public')

    const settings: Record<string, string> = {}
    for (const row of rows ?? []) {
      if ((PUBLIC_KEYS as readonly string[]).includes(row.key) && row.value) {
        settings[row.key] = row.value
      }
    }
    return settings
  } catch {
    // Backend unreachable, or too old to have this endpoint. Not an error here:
    // the storefront must render either way.
    return {}
  }
})
