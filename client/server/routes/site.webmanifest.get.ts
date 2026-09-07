/**
 * GET /site.webmanifest
 *
 * The web app manifest, built from the operator's branding rather than shipped as a file.
 *
 * This replaced a static `public/site.webmanifest` that hardcoded one company's name and
 * listed `/android-chrome-192x192.png` and `/android-chrome-512x512.png` — neither of which
 * has ever existed in `public/`. Every install therefore advertised an installable app whose
 * icons 404, and no amount of uploading a favicon in the admin panel changed it, because
 * nothing here read the settings.
 *
 * Now the icons come from the same uploaded set the `<link rel="icon">` tags use. With
 * nothing uploaded the `icons` array is empty rather than full of broken paths: a manifest
 * that promises no icon is honest, and the browser falls back to the `<link>` tags, while one
 * that promises a missing icon makes the install prompt fail.
 */

/** Sizes an installable PWA is expected to declare, and the file each maps to. */
const MANIFEST_ICON_SIZES = [
  { file: 'android-chrome-192x192.png', sizes: '192x192' },
  { file: 'android-chrome-512x512.png', sizes: '512x512' },
] as const

/**
 * Prefix every uploaded branding URL starts with.
 *
 * Duplicated from `utils/brandingIcons.ts` on purpose: that file is auto-imported into the
 * Vue app, and Nitro routes do not share those auto-imports. One shared constant would have
 * to move to `server/utils/`, which the app side then could not reach — so the string is
 * written twice and each copy says so.
 */
const UPLOADS_PREFIX = '/uploads/branding/'

export default defineEventHandler(async (event) => {
  // No baseURL on purpose. Nitro's $fetch dispatches a leading-slash path straight into this
  // same process; giving it an absolute origin instead sends a real HTTP request, which on a
  // deployed host means the container resolving its own public hostname, hairpinning through
  // nginx and validating the certificate from the inside. That fails in production and nowhere
  // else, and the catch below would bury it as an empty icon list.
  const settings = await $fetch<Record<string, string>>('/api/portal/public/settings')
    .catch(() => ({} as Record<string, string>))

  const favicon = (settings['portal.favicon'] ?? '').trim()

  // Only an upload has a generated set beside it; a pasted URL is one file on someone else's
  // server, and guessing sibling names against it would list icons that 404.
  const icons = favicon.startsWith(UPLOADS_PREFIX)
    ? MANIFEST_ICON_SIZES.map(icon => ({
        src: `${favicon.slice(0, favicon.lastIndexOf('/') + 1)}${icon.file}`,
        sizes: icon.sizes,
        type: 'image/png',
      }))
    : []

  setResponseHeaders(event, {
    'Content-Type': 'application/manifest+json',
    // Short: the manifest follows the branding settings, which an operator changes and
    // expects to see. Long enough that a page load does not re-fetch it.
    'Cache-Control': 'public, max-age=60',
  })

  return {
    // There is no operator-settable site name today, so this stays the product's own. When
    // one is added it belongs here and in the storefront's <title>, not in this file alone.
    name: 'Hostpanel',
    short_name: 'Hostpanel',
    icons,
    theme_color: '#0ea5e9',
    background_color: '#1e3a5f',
    display: 'standalone',
  }
})
