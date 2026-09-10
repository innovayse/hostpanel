/**
 * The typefaces an operator can pick, with the stack the admin renders each option in.
 *
 * Mirrors `client/utils/brandFonts.ts` and `PortalSettingKeys.FontNames` on the backend —
 * the values are what the API accepts for `portal.brand.font_heading` / `font_body`. The
 * faces are the same `@fontsource` packages the storefront ships, imported in `main.css` so
 * an option previews in its own face without a request to a font CDN.
 */

/** A typeface an operator can pick. */
export type BrandFont = 'inter' | 'noto-sans-armenian' | 'noto-serif-armenian' | 'system'

/** One option in the typography card. */
export interface BrandFontOption {
  /** The setting value. */
  value: BrandFont
  /** The i18n key of the option's label. */
  labelKey: string
  /** The CSS stack to preview it in. */
  stack: string
}

/** The options, in the order the card lists them. */
export const BRAND_FONT_OPTIONS: BrandFontOption[] = [
  { value: 'inter', labelKey: 'settings.typography.fonts.inter', stack: "'Inter Variable', Inter, 'Noto Sans Armenian', system-ui, sans-serif" },
  { value: 'noto-sans-armenian', labelKey: 'settings.typography.fonts.notoSansArmenian', stack: "'Noto Sans Armenian', system-ui, sans-serif" },
  { value: 'noto-serif-armenian', labelKey: 'settings.typography.fonts.notoSerifArmenian', stack: "'Noto Serif Armenian', Georgia, serif" },
  { value: 'system', labelKey: 'settings.typography.fonts.system', stack: "system-ui, -apple-system, 'Segoe UI', Roboto, 'Noto Sans Armenian', sans-serif" },
]

/**
 * The preview stack for a setting value.
 *
 * @param value - Raw setting value; empty or unknown means "the template's own".
 * @returns The stack, or `''` to inherit.
 */
export function previewStack(value: string): string {
  return BRAND_FONT_OPTIONS.find(o => o.value === value)?.stack ?? ''
}
