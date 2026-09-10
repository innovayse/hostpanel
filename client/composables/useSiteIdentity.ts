/**
 * The name this storefront calls itself by, set by the operator.
 *
 * `Innovayse` used to be written into `nuxt.config.ts`, `useSchemaOrg.ts` and every
 * logo's `alt` — a self-hosted operator, the audience of this repository, could not
 * take the vendor's name off their own site without editing source. It is still the
 * fallback so an install that has not set a name changes nothing.
 */
export const useSiteIdentity = () => {
  const { get } = usePortalSettings()

  /** The site name for titles, `og:site_name`, `alt` text and structured data. */
  const name = computed(() => get('portal.site.name', 'portalSiteName') || 'Innovayse')

  /** A one-line description for the default meta description. Empty when unset. */
  const tagline = computed(() => get('portal.site.tagline', 'portalSiteTagline'))

  return { name, tagline }
}
