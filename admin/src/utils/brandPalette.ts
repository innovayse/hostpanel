/**
 * Copy of `client/utils/brandPalette.ts` for the admin panel's preview.
 *
 * The admin is built from its own directory (`COPY admin/ .` in docker/admin.Dockerfile),
 * so it cannot import across the repository. The two files are kept identical below this
 * header; `client/utils/brandPalette.shared.test.ts` fails when they drift. Change the
 * client file first, then copy it here.
 */
/**
 * Turns the one colour an operator picks into everything a theme needs from it: the
 * eleven-step scale Tailwind utilities expect, and the text colour that stays readable on
 * top of it.
 *
 * Kept free of Nuxt so the storefront (server-side, into `:root`), the admin preview and
 * the e-mail renderer all agree. The e-mail side is `BrandPalette.cs` on the backend, a
 * port of this file; the two carry the same test numbers and are changed together.
 *
 * The ramp is built in OKLCH: lightness is spread from near-white (50) to near-black
 * (950) while hue stays put and chroma tapers toward the ends, which is what keeps a
 * yellow from turning brown at 800 and a blue from going grey at 200. The picked colour
 * is pinned at 500, so `bg-primary-500` is exactly what the operator chose.
 *
 * @module utils/brandPalette
 */

/** One shade as sRGB 0–255 integers. */
export type Rgb = readonly [number, number, number]

/** The Tailwind shade steps, in order. */
export const SHADE_STEPS = [50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950] as const

/** A shade step. */
export type ShadeStep = (typeof SHADE_STEPS)[number]

/** A full scale keyed by step. */
export type Scale = Record<ShadeStep, Rgb>

/**
 * OKLCH lightness for each step. 500 is replaced by the picked colour's own lightness,
 * and the steps either side are re-spaced so the ramp stays monotonic whatever was picked.
 */
const TARGET_L: Record<ShadeStep, number> = {
  50: 0.975, 100: 0.945, 200: 0.89, 300: 0.81, 400: 0.72,
  500: 0.62, 600: 0.53, 700: 0.45, 800: 0.38, 900: 0.31, 950: 0.22,
}

/**
 * Parses `#rrggbb` (any case) into sRGB integers.
 *
 * @param hex The colour, with its `#`.
 * @returns The channels, or `null` when the string is not a six-digit hex colour.
 */
export function hexToRgb(hex: string): Rgb | null {
  const m = /^#([0-9a-f]{6})$/i.exec(hex.trim())
  if (!m) return null
  const n = parseInt(m[1]!, 16)
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255]
}

/**
 * Formats sRGB integers as lower-case `#rrggbb`.
 *
 * @param rgb The channels.
 * @returns The hex string.
 */
export function rgbToHex([r, g, b]: Rgb): string {
  return '#' + [r, g, b].map(c => c.toString(16).padStart(2, '0')).join('')
}

/** sRGB 0–255 → linear 0–1. */
const toLinear = (c: number): number => {
  const s = c / 255
  return s <= 0.04045 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4
}

/** Linear 0–1 → sRGB 0–255, clamped. */
const toSrgb = (l: number): number => {
  const c = l <= 0.0031308 ? l * 12.92 : 1.055 * l ** (1 / 2.4) - 0.055
  return Math.round(Math.min(1, Math.max(0, c)) * 255)
}

/** sRGB → OKLab (Björn Ottosson's matrices). */
function rgbToOklab([r, g, b]: Rgb): [number, number, number] {
  const lr = toLinear(r), lg = toLinear(g), lb = toLinear(b)
  const l = Math.cbrt(0.4122214708 * lr + 0.5363325363 * lg + 0.0514459929 * lb)
  const m = Math.cbrt(0.2119034982 * lr + 0.6806995451 * lg + 0.1073969566 * lb)
  const s = Math.cbrt(0.0883024619 * lr + 0.2817188376 * lg + 0.6299787005 * lb)
  return [
    0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
    1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
    0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s,
  ]
}

/** OKLab → linear sRGB, unclamped, so a caller can tell whether the colour is in gamut. */
function oklabToLinear([L, a, b]: [number, number, number]): [number, number, number] {
  const l = (L + 0.3963377774 * a + 0.2158037573 * b) ** 3
  const m = (L - 0.1055613458 * a - 0.0638541728 * b) ** 3
  const s = (L - 0.0894841775 * a - 1.2914855480 * b) ** 3
  return [
    +4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
    -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
    -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s,
  ]
}

/** Whether every linear channel is displayable, with a hair of tolerance for rounding. */
const inGamut = (lin: [number, number, number]): boolean =>
  lin.every(c => c >= -0.0005 && c <= 1.0005)

/**
 * OKLCH → sRGB, mapped into gamut by reducing chroma at fixed lightness and hue.
 *
 * Clamping channels instead — what a naive conversion does — turns a light tint of a
 * saturated blue into cyan and a light yellow into acid green, because the clipped channel
 * changes the hue. Pulling chroma back until the colour fits keeps the hue the operator
 * chose; the tint just gets a little paler, which is what a tint is.
 */
function oklchToRgb(L: number, chroma: number, hue: number): Rgb {
  const at = (c: number): [number, number, number] =>
    oklabToLinear([L, c * Math.cos(hue), c * Math.sin(hue)])

  let lin = at(chroma)
  if (!inGamut(lin)) {
    let lo = 0, hi = chroma
    for (let i = 0; i < 20; i++) {
      const mid = (lo + hi) / 2
      if (inGamut(at(mid))) lo = mid
      else hi = mid
    }
    lin = at(lo)
  }
  return [toSrgb(lin[0]), toSrgb(lin[1]), toSrgb(lin[2])]
}

/**
 * Builds the eleven-step scale around one colour.
 *
 * @param hex The picked colour, `#rrggbb`.
 * @returns The scale, with the picked colour itself at 500.
 * @throws {RangeError} When `hex` is not a six-digit hex colour.
 */
export function buildScale(hex: string): Scale {
  const rgb = hexToRgb(hex)
  if (!rgb) throw new RangeError(`Not a #rrggbb colour: ${hex}`)

  const [L0, a0, b0] = rgbToOklab(rgb)
  const chroma0 = Math.hypot(a0, b0)
  const hue = Math.atan2(b0, a0)

  const scale = {} as Scale
  for (const step of SHADE_STEPS) {
    if (step === 500) {
      scale[step] = rgb
      continue
    }
    // Re-space the target lightness so the picked colour sits at 500 and the ramp stays
    // monotonic: steps above 500 interpolate between L0 and white, steps below between
    // L0 and black, each keeping its relative position from the reference table.
    // The ends move with the picked colour: a near-black brand still gets a darker 950 and a
    // near-white one a lighter 50, so the ramp never flattens into equal steps.
    const top = Math.max(0.985, L0 + (1 - L0) * 0.6)
    const bottom = Math.min(0.15, L0 * 0.6)
    const ref = TARGET_L[step]
    const ref500 = TARGET_L[500]
    const L = step < 500
      ? L0 + (ref - ref500) / (TARGET_L[50] - ref500) * (top - L0)
      : L0 - (ref500 - ref) / (ref500 - TARGET_L[950]) * (L0 - bottom)
    // Chroma tapers toward both ends so the light steps read as tints rather than pastel
    // neon and the dark ones do not go muddy. Tints taper harder (to ~8% at 50) than shades
    // (to ~25% at 950): a saturated near-white is out of sRGB gamut and clamps to cyan or
    // acid yellow, while a dark shade keeps its identity with more colour left in it.
    const distance = Math.min(1, Math.abs(L - L0) / Math.max(L0 - bottom, top - L0))
    const taper = step < 500 ? 0.92 : 0.75
    const chroma = chroma0 * (1 - taper * distance ** 0.8)
    scale[step] = oklchToRgb(L, chroma, hue)
  }
  return scale
}

/**
 * WCAG 2 relative luminance.
 *
 * @param rgb The channels.
 * @returns Luminance in 0–1.
 */
export function luminance([r, g, b]: Rgb): number {
  return 0.2126 * toLinear(r) + 0.7152 * toLinear(g) + 0.0722 * toLinear(b)
}

/**
 * WCAG 2 contrast ratio between two colours, 1–21.
 *
 * @param a One colour, `#rrggbb`.
 * @param b The other, `#rrggbb`.
 * @returns The ratio, or `NaN` when either string is not a colour.
 */
export function contrastRatio(a: string, b: string): number {
  const ra = hexToRgb(a), rb = hexToRgb(b)
  if (!ra || !rb) return NaN
  const la = luminance(ra), lb = luminance(rb)
  const [hi, lo] = la > lb ? [la, lb] : [lb, la]
  return (hi + 0.05) / (lo + 0.05)
}

/** The AA threshold for normal text. */
export const WCAG_AA = 4.5

/** The AA threshold for large or bold text, and the point below which white stops being an option. */
export const WCAG_AA_LARGE = 3

/**
 * The text colour to put on a filled surface of the given colour.
 *
 * White wins whenever it reaches the large-text threshold (3:1) — buttons are bold, and
 * white on a mid blue or purple is what every design system ships even where dark text
 * would score a little higher. Below that, whichever of white and near-black reads better:
 * a brand yellow or a pale teal gets dark text.
 *
 * @param hex The surface colour, `#rrggbb`.
 * @returns `#ffffff` or `#111111`.
 */
export function bestTextOn(hex: string): '#ffffff' | '#111111' {
  const white = contrastRatio('#ffffff', hex)
  if (white >= WCAG_AA_LARGE) return '#ffffff'
  return white >= contrastRatio('#111111', hex) ? '#ffffff' : '#111111'
}

/**
 * Renders a scale as the `--<name>-<step>: r g b` declarations the stylesheets read, in
 * the space-separated form Tailwind's `<alpha-value>` slot needs.
 *
 * @param name The variable stem, e.g. `brand-primary`.
 * @param scale The scale.
 * @returns One declaration per step, joined with `;`.
 */
export function scaleToDeclarations(name: string, scale: Scale): string {
  return SHADE_STEPS.map(step => `--${name}-${step}:${scale[step].join(' ')}`).join(';')
}
