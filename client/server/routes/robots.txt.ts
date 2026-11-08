/**
 * GET and HEAD /robots.txt
 *
 * The crawler rules, built per request so the `Sitemap:` line carries the operator's own origin
 * (the configured public address, else the one the request came in on, honouring a proxy's
 * forwarded host). A static file could not name a sitemap without hard-coding one deployment's
 * domain.
 *
 * One `*` group on purpose: a crawler that matches a named group ignores `*`, so a per-bot group
 * that only said `Allow: /` would let that bot into the private areas.
 *
 * @module server/routes/robots.txt
 */

import { defineReadOnlyEventHandler } from '~/server/utils/readOnlyEventHandler'

/** Areas that belong to a signed-in visitor and must not be crawled, before the locale prefix. */
const PRIVATE_AREAS = ['client', 'cart', 'checkout', 'order'] as const

/** Locale URL prefixes the private areas also live under (the default language has none). */
const LOCALE_PREFIXES = ['', '/ru', '/hy'] as const

/** Static assets that stay crawlable, so pages render fully for the crawler. */
const ASSET_EXTENSIONS = [
  'css', 'js', 'json', 'xml', 'jpg', 'jpeg', 'png', 'gif', 'svg', 'webp', 'ico',
  'woff', 'woff2', 'ttf', 'eot', 'mp4', 'webm'
] as const

/**
 * Builds the robots.txt body for an origin.
 *
 * @param origin - The deployment's public origin, without a trailing slash.
 * @returns The robots.txt text, ending with the sitemap line.
 */
const buildRobotsTxt = (origin: string): string => {
  const lines = ['User-agent: *']
  for (const prefix of LOCALE_PREFIXES) {
    for (const area of PRIVATE_AREAS) {
      lines.push(`Disallow: ${prefix}/${area}/`)
    }
  }
  lines.push('Allow: /')
  for (const extension of ASSET_EXTENSIONS) {
    lines.push(`Allow: /*.${extension}$`)
  }
  lines.push('', `Sitemap: ${origin}/sitemap.xml`, '')
  return lines.join('\n')
}

/** Handler: the robots.txt text for the operator's origin, served as plain text. */
export default defineReadOnlyEventHandler((event) => {
  const config = useRuntimeConfig()
  const origin = (config.public.baseUrl as string | undefined)
    || getRequestURL(event, { xForwardedHost: true, xForwardedProto: true }).origin
  setResponseHeaders(event, { 'Content-Type': 'text/plain; charset=utf-8' })
  return buildRobotsTxt(origin.replace(/\/+$/, ''))
})
