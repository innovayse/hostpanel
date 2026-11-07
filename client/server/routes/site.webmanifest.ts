/**
 * GET and HEAD /site.webmanifest
 *
 * The web app manifest, built from the operator's branding rather than shipped as a file.
 *
 * A static file would carry one name and fixed icons whatever the operator uploaded. The
 * icons come from the same set the `<link rel="icon">` tags use: the uploaded one when
 * there is an upload, and the committed `public/` set otherwise. Both exist on disk,
 * so the install prompt has something real to show either way.
 */

import { PortalSettingKey } from '~/constants/portalSettingKey'
import { defineReadOnlyEventHandler } from '~/server/utils/readOnlyEventHandler'

/** Sizes an installable PWA is expected to declare, and the file each maps to. */
const MANIFEST_ICON_SIZES = [
  { file: 'icon-192.png', sizes: '192x192', purpose: 'any' },
  { file: 'icon-512.png', sizes: '512x512', purpose: 'any' },
  { file: 'icon-maskable-192.png', sizes: '192x192', purpose: 'maskable' },
  { file: 'icon-maskable-512.png', sizes: '512x512', purpose: 'maskable' },
] as const

/**
 * Prefix every uploaded branding URL starts with.
 *
 * Duplicated from `utils/brandingIcons.ts` on purpose: that file is auto-imported into the
 * Vue app, and Nitro routes do not share those auto-imports. One shared constant would have
 * to move to `server/utils/`, which the app side then could not reach, so the string is
 * written twice and each copy says so.
 */
const UPLOADS_PREFIX = '/uploads/branding/'

export default defineReadOnlyEventHandler(async (event) => {
  // No baseURL on purpose. Nitro's $fetch dispatches a leading-slash path straight into this
  // same process; giving it an absolute origin instead sends a real HTTP request, which on a
  // deployed host means the container resolving its own public hostname, hairpinning through
  // nginx and validating the certificate from the inside. That fails in production and nowhere
  // else, and the catch below would bury it as an empty icon list.
  const settings = await $fetch<Record<string, string>>('/api/portal/public/settings')
    .catch(() => ({} as Record<string, string>))

  const favicon = (settings[PortalSettingKey.Favicon] ?? '').trim()

  // An upload has its generated set in its own directory; anything else (nothing set, a URL
  // the operator pasted, or an upload from before the generator existed) falls back to the
  // committed set in `public/`. Both are real files, so the manifest never promises an icon
  // that 404s. Sibling names are only guessed inside a generated directory, where this process
  // wrote them and knows they are there.
  //
  // The depth check is the whole point: a pre-generator upload sits flat in the uploads root
  // and matches the prefix while having no siblings at all. Testing the prefix by itself put
  // four dead icon URLs into the production manifest.
  const isGeneratedSet = favicon.startsWith(UPLOADS_PREFIX)
    && favicon.slice(UPLOADS_PREFIX.length).split('/').length >= 3

  const icons = isGeneratedSet
    ? MANIFEST_ICON_SIZES.map(icon => ({
        src: `${favicon.slice(0, favicon.lastIndexOf('/') + 1)}${icon.file}`,
        sizes: icon.sizes,
        type: 'image/png',
        purpose: icon.purpose,
      }))
    : MANIFEST_ICON_SIZES.map(icon => ({
        src: `/${icon.file}`,
        sizes: icon.sizes,
        type: 'image/png',
        purpose: icon.purpose,
      }))

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
    theme_color: '#0B0B14',
    background_color: '#0B0B14',
    display: 'standalone',
  }
})
