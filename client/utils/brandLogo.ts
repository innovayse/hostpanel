/**
 * Which operator-uploaded logo to show, kept free of Nuxt so the rule can be
 * unit-tested. `useBrandLogo()` is the only caller.
 *
 * @module utils/brandLogo
 */

/** The three images an operator can upload, each empty when not set. */
export interface BrandLogoSources {
  /** `portal.logo` — the logo for light backgrounds, and the one every install had first. */
  light: string
  /** `portal.logo.dark` — the logo for dark backgrounds. */
  dark: string
  /** `portal.logo.mark` — the square icon-only logo. */
  mark: string
}

/**
 * Picks the wordmark logo for the current mode.
 *
 * Dark mode uses the dark logo when there is one and the light logo otherwise; light
 * mode always uses the light logo. An install that uploaded only `portal.logo` keeps
 * rendering it in both modes, exactly as before the dark variant existed.
 *
 * @param sources The uploaded images.
 * @param isDark Whether the page is in dark mode.
 * @returns The URL to render, or `''` when nothing was uploaded.
 */
export function pickBrandLogo(sources: BrandLogoSources, isDark: boolean): string {
  if (isDark && sources.dark) return sources.dark
  return sources.light
}

/** The two wordmark URLs a template renders side by side, swapped by CSS. */
export interface BrandLogoPair {
  /** For light backgrounds: `portal.logo`, falling back to the dark one, or `''`. */
  light: string
  /** For dark backgrounds: `portal.logo.dark`, falling back to the light one, or `''`. */
  dark: string
  /** Whether the two differ, so a template knows to render both. */
  hasDarkVariant: boolean
}

/**
 * Resolves both wordmarks with their fallbacks, for a template that renders one
 * `<img>` per mode and lets the `dark:` variant decide which is visible.
 *
 * CSS rather than {@link pickBrandLogo} on the page: under the `system` default the
 * server does not know the mode, renders as dark, and a browser on a light device
 * would keep the server's `src` — production Vue does not patch an attribute that
 * disagrees after hydration. Two images and a class swap need no JavaScript and can
 * never disagree with the palette.
 *
 * @param sources The uploaded images.
 * @returns Both URLs and whether they differ.
 */
export function resolveBrandLogos(sources: BrandLogoSources): BrandLogoPair {
  // Each side falls back to the other: an operator who uploaded only one logo gets it
  // in both modes, which is what every install had before the dark variant existed.
  const light = sources.light || sources.dark
  const dark = sources.dark || sources.light
  return { light, dark, hasDarkVariant: dark !== light }
}

/**
 * Picks the compact logo for places too narrow for a wordmark.
 *
 * The mark when uploaded; otherwise the wordmark for the current mode, which is
 * wrong-shaped but still the operator's brand; otherwise `''`, which callers render
 * as the built-in mark.
 *
 * @param sources The uploaded images.
 * @param isDark Whether the page is in dark mode.
 * @returns The URL to render, or `''` when nothing was uploaded.
 */
export function pickBrandMark(sources: BrandLogoSources, isDark: boolean): string {
  return sources.mark || pickBrandLogo(sources, isDark)
}
