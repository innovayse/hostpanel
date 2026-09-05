/**
 * Derives the full browser/iOS/Android icon set from the stored favicon URL.
 *
 * The `portal.favicon` setting holds one URL. When an operator uploaded the image, the API
 * generated a whole set beside it — every file of one upload lands in the same directory,
 * under names the backend chooses. This module is the storefront's half of that contract.
 *
 * The names are fixed by `SkiaBrandingImageProcessor.RenderFaviconSet` in
 * `backend/src/Innovayse.Infrastructure/Branding/`. **Renaming a file there without changing
 * `UPLOADED_ICON_SET` here produces no error anywhere** — the tags are emitted, the browser
 * requests them, the proxy answers 404, and the tab silently keeps the previous icon. If you
 * touch one side, touch both.
 */

/** One icon the storefront links, and how it is linked. */
export interface BrandingIcon {
  /** Value for the tag's `rel` attribute. */
  rel: string
  /** Absolute-from-root URL of the file. */
  href: string
  /** Media type, so the browser can skip formats it cannot render. */
  type: string
  /** `WxH` for the `sizes` attribute; omitted for a link that takes no size. */
  sizes?: string
}

/**
 * Prefix every uploaded branding URL starts with.
 *
 * Membership of this path is what tells an uploaded favicon apart from a URL the operator
 * pasted by hand. A pasted URL points at a single file on someone else's server, so guessing
 * sibling names against it would emit a page full of broken icon links.
 */
const UPLOADS_PREFIX = '/uploads/branding/'

/**
 * The files generated beside an uploaded favicon, and the tag each becomes.
 *
 * Mirrors the backend's `_faviconSet`, minus the sizes no tag references.
 */
const UPLOADED_ICON_SET: readonly { file: string, rel: string, sizes?: string }[] = [
  { file: 'favicon-16x16.png', rel: 'icon', sizes: '16x16' },
  { file: 'favicon-32x32.png', rel: 'icon', sizes: '32x32' },
  { file: 'favicon-48x48.png', rel: 'icon', sizes: '48x48' },
  { file: 'apple-touch-icon.png', rel: 'apple-touch-icon', sizes: '180x180' },
  { file: 'android-chrome-192x192.png', rel: 'icon', sizes: '192x192' },
  { file: 'android-chrome-512x512.png', rel: 'icon', sizes: '512x512' },
]

/**
 * The icons a fresh install serves, straight out of `public/`.
 *
 * These used to live in `nuxt.config.ts`. They were moved here because two places emitting
 * `<link rel="icon">` is one too many: unhead only dedupes link tags that carry a matching
 * explicit key, so a static tag in the config and an uploaded one from `app.vue` both survive
 * into the rendered head, and Chrome prefers the SVG — an operator who uploaded a favicon kept
 * seeing the built-in mark and had no way to tell why.
 */
const BUILT_IN_ICONS: BrandingIcon[] = [
  { rel: 'icon', href: '/favicon.svg', type: 'image/svg+xml' },
  { rel: 'icon', href: '/favicon.ico', type: 'image/x-icon' },
]

/**
 * Whether a favicon URL points at a file this deployment generated the set for.
 *
 * @param url The stored `portal.favicon` value.
 * @returns True when the URL is one of our own uploads.
 */
export const isUploadedBranding = (url: string): boolean =>
  url.startsWith(UPLOADS_PREFIX)

/**
 * Builds the icon links for a stored favicon URL.
 *
 * @param faviconUrl The stored `portal.favicon` value; empty when nothing is set.
 * @returns
 *   The links to emit. Empty when nothing is set, so the caller leaves `nuxt.config`'s static
 *   defaults in place. A single link for a pasted URL, because only that one file is known to
 *   exist. The full set for an upload.
 */
export const brandingIcons = (faviconUrl: string): BrandingIcon[] => {
  if (!faviconUrl) return BUILT_IN_ICONS

  if (!isUploadedBranding(faviconUrl)) {
    // Someone else's file. It may be an .ico, an .svg or a .png — the browser will work it
    // out, and declaring a type we have not checked would be worse than declaring none.
    return [{ rel: 'icon', href: faviconUrl, type: '' }]
  }

  const directory = faviconUrl.slice(0, faviconUrl.lastIndexOf('/') + 1)

  return UPLOADED_ICON_SET.map(icon => ({
    rel: icon.rel,
    href: `${directory}${icon.file}`,
    type: 'image/png',
    sizes: icon.sizes,
  }))
}
