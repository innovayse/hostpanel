/**
 * Colour helpers for kit components that paint a colour known only at run time (a plugin's
 * declared colour), where a Tailwind class cannot be used.
 *
 * Holds the named colour constants and the contrast rule that picks a readable text colour.
 */

/** Neutral grey block colour used when no colour is declared. */
export const NEUTRAL_COLOR = '#64748b'

/** Light text colour, used on dark backgrounds. */
export const LIGHT_TEXT_COLOR = '#ffffff'

/** Dark text colour, used on light backgrounds. */
export const DARK_TEXT_COLOR = '#0f172a'

/** Matches a six-digit hex colour with its `#`. */
const HEX_COLOR = /^#([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})$/i

/** Largest value of an 8-bit colour channel. */
const CHANNEL_MAX = 255

/** sRGB value above which the gamma curve is applied (WCAG 2.x). */
const LINEAR_THRESHOLD = 0.03928

/**
 * Converts one 8-bit sRGB channel to its linear light value.
 *
 * @param channel - Channel value from 0 to 255.
 * @returns Linear value from 0 to 1.
 */
const linearChannel = (channel: number): number => {
  const value = channel / CHANNEL_MAX
  return value <= LINEAR_THRESHOLD ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4
}

/**
 * Computes the WCAG relative luminance of a hex colour.
 *
 * @param hex - Colour as `#rrggbb`.
 * @returns Luminance from 0 (black) to 1 (white), or null when the text is not a six-digit hex colour.
 */
export const relativeLuminance = (hex: string): number | null => {
  const match = HEX_COLOR.exec(hex.trim())
  if (!match) {
    return null
  }
  const [r = 0, g = 0, b = 0] = [match[1], match[2], match[3]].map((part) => linearChannel(parseInt(part ?? '0', 16)))
  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}

/**
 * Computes the WCAG contrast ratio between two luminances.
 *
 * @param a - First luminance.
 * @param b - Second luminance.
 * @returns Ratio from 1 (identical) to 21 (black on white).
 */
const contrastRatio = (a: number, b: number): number => (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05)

/**
 * Picks the text colour (light or dark) with the higher contrast against a background colour.
 *
 * @param background - Background as `#rrggbb`.
 * @returns The light or dark text colour; the light one when the background is not a hex colour.
 */
export const readableTextColor = (background: string): string => {
  const bg = relativeLuminance(background)
  const light = relativeLuminance(LIGHT_TEXT_COLOR)
  const dark = relativeLuminance(DARK_TEXT_COLOR)
  if (bg === null || light === null || dark === null) {
    return LIGHT_TEXT_COLOR
  }
  return contrastRatio(bg, light) >= contrastRatio(bg, dark) ? LIGHT_TEXT_COLOR : DARK_TEXT_COLOR
}
