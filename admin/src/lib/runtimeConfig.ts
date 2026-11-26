/**
 * Runtime configuration helpers: the deployment's storefront and documentation addresses from `config.js`, and URLs for stored storefront images.
 */

import type { AppRuntimeConfig } from '@/types/runtimeConfig'

/**
 * Normalises a configured origin: trimmed, no trailing slash, `''` for empty input.
 *
 * @param raw - The configured value, possibly missing.
 * @returns The origin without a trailing slash, or `''`.
 */
export const normalizeOrigin = (raw: string | undefined): string => (raw ?? '').trim().replace(/\/+$/, '')

/**
 * Reads the runtime configuration injected by `config.js`.
 *
 * @returns The configuration with every field normalised; empty addresses when `config.js` is absent.
 */
export const getRuntimeConfig = (): AppRuntimeConfig => ({
  clientBaseUrl: normalizeOrigin(window.__APP_CONFIG__?.clientBaseUrl),
  docsUrl: normalizeOrigin(window.__APP_CONFIG__?.docsUrl),
})

/** Path prefix of operator uploads, which the API serves and the admin's own origin proxies to it. */
const UPLOADS_PREFIX = '/uploads/'

/**
 * Resolves an image URL stored by the storefront to one the admin can load.
 *
 * The storefront saves its own static assets as root-relative paths (`/demo/partners/a.svg`), which
 * only resolve on the storefront's origin; on the admin's origin they hit the SPA fallback and
 * answer with the admin page. Operator uploads (`/uploads/...`) are the exception: the API serves
 * them and the admin's origin proxies them (dev server and edge alike), so they stay relative and
 * the admin never depends on the storefront being reachable to preview what was just uploaded. Absolute, protocol-relative, `data:` and `blob:` URLs are already
 * loadable and pass through, as does a relative path when no storefront origin is configured.
 *
 * @param url - The stored URL, possibly empty.
 * @returns The URL to put in an `src`.
 */
export const storefrontAssetUrl = (url: string): string => {
  const { clientBaseUrl } = getRuntimeConfig()
  if (!url || !clientBaseUrl || !url.startsWith('/') || url.startsWith('//') || url.startsWith(UPLOADS_PREFIX)) {
    return url
  }
  return `${clientBaseUrl}${url}`
}
