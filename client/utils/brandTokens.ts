/**
 * Builds the `:root` declarations the storefront emits for the operator's brand — colour
 * scales, the text colours that sit on them, and the typefaces — from the raw settings.
 *
 * Pure, so `plugins/brand-tokens.ts` can call it on the server and a test can assert the
 * exact CSS. Returns `''` when nothing is set, and the plugin then emits no `<style>` at
 * all: the stylesheets' `var(--x, <fallback>)` take over and the page is what it was
 * before these settings existed.
 *
 * @module utils/brandTokens
 */

import { bestTextOn, buildScale, hexToRgb, rgbToHex, scaleToDeclarations } from '~/utils/brandPalette'
import { fontStack } from '~/utils/brandFonts'

/** The raw setting values the tokens are built from; each may be empty. */
export interface BrandSettings {
  primary: string
  accent: string
  fontHeading: string
  fontBody: string
}

/**
 * Turns a `#rrggbb` into the `r g b` form Tailwind's `<alpha-value>` slot needs.
 *
 * @param hex The colour.
 * @returns Space-separated channels.
 */
const channels = (hex: string): string => (hexToRgb(hex) ?? [0, 0, 0]).join(' ')

/**
 * The `:root { … }` rule for the given settings, or `''` when every setting is empty.
 *
 * A colour that fails to parse is ignored rather than thrown on: the API already refuses
 * a malformed value, so this only happens with a hand-edited row or an older backend, and
 * a storefront must render either way.
 *
 * @param s The settings.
 * @returns A complete CSS rule, or `''`.
 */
export function brandTokensCss(s: BrandSettings): string {
  const parts: string[] = []

  const primary = hexToRgb(s.primary) ? s.primary.toLowerCase() : ''
  const accent = hexToRgb(s.accent) ? s.accent.toLowerCase() : ''

  if (primary) {
    const scale = buildScale(primary)
    parts.push(scaleToDeclarations('brand-primary', scale))
    // Text on a filled primary-500 (buttons in the shared components and classic), and on
    // the lighter 400 that aurora's gradient and nova's dark-mode brand are built from.
    parts.push(`--brand-on-primary:${channels(bestTextOn(primary))}`)
    parts.push(`--brand-on-tint:${channels(bestTextOn(rgbToHex(scale[400])))}`)
  }

  if (accent) {
    const scale = buildScale(accent)
    parts.push(scaleToDeclarations('brand-accent', scale))
    // Nova fills badges and the pricing toggle with the accent — its 500 in light mode, the
    // 400 tint in dark — and needs to know what text survives on each.
    parts.push(`--brand-on-accent:${channels(bestTextOn(accent))}`)
    parts.push(`--brand-on-accent-tint:${channels(bestTextOn(rgbToHex(scale[400])))}`)
  }

  const heading = fontStack(s.fontHeading)
  const body = fontStack(s.fontBody)
  if (heading) parts.push(`--font-heading:${heading}`)
  if (body) parts.push(`--font-body:${body}`)

  return parts.length ? `:root{${parts.join(';')}}` : ''
}
