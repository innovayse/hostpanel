/**
 * Runtime configuration helpers: the deployment base path and origin from `config.js`, and URLs for public files and stored storefront images.
 */

import type { AppRuntimeConfig } from '@/types/runtimeConfig'

/**
 * Normalises a raw base path: a leading slash, no trailing slash, `/` for empty input.
 *
 * @param raw - The configured value, possibly missing or malformed.
 * @returns The canonical base path (`/` or `/segment[/segment]`).
 */
export const normalizeBasePath = (raw: string | undefined): string => {
  const trimmed = (raw ?? '').trim().replace(/\/+$/, '')
  if (trimmed === '') {
    return '/'
  }
  return trimmed.startsWith('/') ? trimmed : `/${trimmed}`
}

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
 * @returns The configuration with every field normalised; the root base when `config.js` is absent.
 */
export const getRuntimeConfig = (): AppRuntimeConfig => ({
  basePath: normalizeBasePath(window.__APP_CONFIG__?.basePath),
  clientBaseUrl: normalizeOrigin(window.__APP_CONFIG__?.clientBaseUrl),
  docsUrl: normalizeOrigin(window.__APP_CONFIG__?.docsUrl),
})

/**
 * Builds the URL of a file in `public/` under the deployment's base path.
 *
 * @param file - Path inside `public/`, without a leading slash.
 * @returns The root-relative URL including the base path.
 */
export const publicAssetUrl = (file: string): string =>
  `${getRuntimeConfig().basePath.replace(/\/+$/, '')}/${file}`

/**
 * Resolves an image URL stored by the storefront to one the admin can load.
 *
 * The storefront saves its own assets as root-relative paths (`/demo/partners/a.svg`), which
 * only resolve on the storefront's origin; on the admin's origin they hit the SPA fallback and
 * answer with the admin page. Absolute, protocol-relative, `data:` and `blob:` URLs are already
 * loadable and pass through, as does a relative path when no storefront origin is configured.
 *
 * @param url - The stored URL, possibly empty.
 * @returns The URL to put in an `src`.
 */
export const storefrontAssetUrl = (url: string): string => {
  const { clientBaseUrl } = getRuntimeConfig()
  if (!url || !clientBaseUrl || !url.startsWith('/') || url.startsWith('//')) {
    return url
  }
  return `${clientBaseUrl}${url}`
}
