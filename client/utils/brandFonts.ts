/**
 * The typefaces an operator can pick, and the CSS stack each one resolves to.
 *
 * Every face here is an `@fontsource` package the storefront already ships and imports
 * in `assets/css/main.css`, so choosing one never causes a request to a font CDN. The
 * names mirror `PortalSettingKeys.FontNames` on the backend, which is what the API
 * accepts for `portal.brand.font_heading` / `font_body`.
 *
 * Every stack ends in Noto Sans Armenian: Inter has no Armenian glyphs, and an
 * Armenian-locale page set to Inter would otherwise fall through to whatever the device
 * has — usually a serif that matches nothing else on the page.
 *
 * @module utils/brandFonts
 */

/** A typeface an operator can pick. */
export type BrandFont = 'inter' | 'noto-sans-armenian' | 'noto-serif-armenian' | 'system'

/** The Armenian fallback every stack ends in. */
const ARMENIAN_FALLBACK = "'Noto Sans Armenian', system-ui, sans-serif"

/** The CSS `font-family` stack per face. */
export const FONT_STACKS: Record<BrandFont, string> = {
  'inter': `'Inter Variable', Inter, ${ARMENIAN_FALLBACK}`,
  'noto-sans-armenian': `'Noto Sans Armenian', Inter, system-ui, sans-serif`,
  'noto-serif-armenian': `'Noto Serif Armenian', Georgia, 'Times New Roman', ${ARMENIAN_FALLBACK}`,
  'system': `system-ui, -apple-system, 'Segoe UI', Roboto, ${ARMENIAN_FALLBACK}`,
}

/** The faces, in the order the admin panel lists them. */
export const BRAND_FONTS = Object.keys(FONT_STACKS) as BrandFont[]

/**
 * Resolves a setting value to a stack.
 *
 * @param value Raw `portal.brand.font_*` value.
 * @returns The stack, or `''` when the value is empty or unknown — the template then keeps its own.
 */
export function fontStack(value: string | null | undefined): string {
  return value && value in FONT_STACKS ? FONT_STACKS[value as BrandFont] : ''
}
