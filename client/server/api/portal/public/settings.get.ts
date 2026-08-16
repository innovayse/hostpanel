/**
 * GET /api/portal/public/settings
 *
 * Returns the `portal.*` settings the storefront needs — the active template and
 * the contact/newsletter widget configuration.
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
  'portal.contact.whatsapp',
  'portal.contact.telegram',
  'portal.chat.provider',
  'portal.newsletter.action_url',
  'portal.contact.email',
] as const

export default defineCachedEventHandler(async (event) => {
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
}, {
  name: 'portal-settings',
  maxAge: 60,
  swr: true,
  getKey: () => 'portal-settings',
})
