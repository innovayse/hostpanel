/**
 * Guards for the design tokens in `src/assets/main.css`: both themes pass WCAG AA for text, the brand
 * ramps are the ones `lib/brandPalette.ts` builds, and the copies of a token that live elsewhere (the
 * chart fallback, the browser chrome colour) still agree with the stylesheet. The tests read the
 * stylesheet itself, so a value cannot be changed there without the numbers being checked again.
 */
import { readdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'
import { buildScale, contrastRatio, rgbToHex, SHADE_STEPS, WCAG_AA, WCAG_AA_LARGE } from '@/lib/brandPalette'
import { THEME_BOOT_CONFIG } from '@/lib/themeBoot'
import { readChartTheme } from '@/utils/ui/chart'

/** The admin root: the folder above `tests/`. */
const ROOT = join(__dirname, '..', '..')

/** The stylesheet with its comments removed, so a brace or a colon in prose is never read as code. */
const CSS = readFileSync(join(ROOT, 'src', 'assets', 'main.css'), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '')

/** The page the browser loads first; its `theme-color` metas must match the stylesheet. */
const INDEX_HTML = readFileSync(join(ROOT, 'index.html'), 'utf8')

/** One channel set with alpha, as 0 to 255 integers and a 0 to 1 alpha. */
type Rgba = readonly [number, number, number, number]

/** Token name (without the dashes) to its declared value. */
type Tokens = Record<string, string>

/**
 * Reads the declarations of one top-level block of the stylesheet.
 *
 * @param header - The block header exactly as written, for example `:root.light`.
 * @returns The custom properties declared in it, keyed without the leading dashes.
 */
const tokensOf = (header: string): Tokens => {
  const start = CSS.indexOf(`\n${header} {`)
  if (start < 0) {
    throw new Error(`No block "${header}" in main.css`)
  }
  const body = CSS.slice(start, CSS.indexOf('\n}', start))
  const tokens: Tokens = {}
  for (const match of body.matchAll(/--([\w-]+):\s*([^;]+);/g)) {
    tokens[match[1] ?? ''] = (match[2] ?? '').replace(/\s+/g, ' ').trim()
  }
  return tokens
}

/** Status colours are declared once for the dark theme in the theme block, then overridden for light. */
const THEME_STATIC = tokensOf('@theme static')

/** Tokens of the dark theme (the default). */
const DARK: Tokens = { ...tokensOf(':root'), ...THEME_STATIC }

/** Tokens of the light theme: the dark set with the light overrides on top. */
const LIGHT: Tokens = { ...DARK, ...tokensOf(':root.light') }

/**
 * Parses `#rrggbb` or `rgb(r g b / a)`.
 *
 * @param value - The declared colour.
 * @returns Channels and alpha.
 */
const parseColor = (value: string): Rgba => {
  const hex = /^#([0-9a-f]{6})$/i.exec(value)
  if (hex) {
    const n = parseInt(hex[1] ?? '0', 16)
    return [(n >> 16) & 255, (n >> 8) & 255, n & 255, 1]
  }
  const rgb = /^rgb\((\d+) (\d+) (\d+)(?: \/ ([\d.]+))?\)$/.exec(value)
  if (!rgb) {
    throw new Error(`Not a colour: ${value}`)
  }
  return [Number(rgb[1]), Number(rgb[2]), Number(rgb[3]), rgb[4] === undefined ? 1 : Number(rgb[4])]
}

/**
 * Paints a possibly translucent colour over an opaque one, as the browser does.
 *
 * @param top - The colour on top.
 * @param under - The opaque colour behind it, `#rrggbb`.
 * @returns The flattened colour, `#rrggbb`.
 */
const over = (top: string, under: string): string => {
  const [r, g, b, a] = parseColor(top)
  const [ur, ug, ub] = parseColor(under)
  /**
   * Blends one channel of the top colour with the same channel behind it.
   *
   * @param front - Channel of the top colour.
   * @param back - Channel of the colour behind.
   * @returns The blended channel.
   */
  const mix = (front: number, back: number): number => Math.round(front * a + back * (1 - a))
  return rgbToHex([mix(r, ur), mix(g, ug), mix(b, ub)])
}

/**
 * The colours text sits on in a theme: the ground, the frosted card over it, a field and a hovered row
 * on that card, and the two opaque surfaces.
 *
 * @param theme - The theme's tokens.
 * @returns Named opaque colours.
 */
const surfaces = (theme: Tokens): Record<string, string> => {
  const base = over(theme['surface-base'] ?? '', '#ffffff')
  const card = over(theme['surface-card'] ?? '', base)
  return {
    base,
    card,
    field: over(theme['surface-field'] ?? '', card),
    hover: over(theme['surface-hover'] ?? '', card),
    panel: over(theme['surface-panel'] ?? '', base),
    elevated: over(theme['surface-elevated'] ?? '', base),
  }
}

/** The text tokens, each of which must be readable on every surface. */
const TEXT_TOKENS = ['text-primary', 'text-secondary', 'text-muted', 'text-accent'] as const

/** The status text tokens and the tint each one sits on in a pill. */
const STATUS_PAIRS = [
  ['color-status-red', 'tint-red'],
  ['color-status-green', 'tint-green'],
  ['color-status-yellow', 'tint-yellow'],
  ['color-status-blue', 'tint-blue'],
] as const

describe.each([['dark', DARK], ['light', LIGHT]] as const)('%s theme contrast', (themeName, theme) => {
  /** The opaque colours text sits on in this theme. */
  const surface = surfaces(theme)

  it.each(TEXT_TOKENS)('%s passes AA on every surface', (token) => {
    const text = over(theme[token] ?? '', surface.base ?? '')
    for (const [surfaceName, color] of Object.entries(surface)) {
      expect(contrastRatio(text, color), `${token} on ${surfaceName}`).toBeGreaterThanOrEqual(WCAG_AA)
    }
  })

  it.each(STATUS_PAIRS)('%s passes AA on the card and on its own tint', (statusToken, tintToken) => {
    const text = over(theme[statusToken] ?? '', surface.card ?? '')
    const tint = over(theme[tintToken] ?? '', surface.card ?? '')
    expect(contrastRatio(text, surface.card ?? ''), `${statusToken} on card`).toBeGreaterThanOrEqual(WCAG_AA)
    expect(contrastRatio(text, tint), `${statusToken} on ${tintToken}`).toBeGreaterThanOrEqual(WCAG_AA)
  })

  it('keeps the status tints at 14 percent', () => {
    for (const [, tintToken] of STATUS_PAIRS) {
      expect(parseColor(theme[tintToken] ?? '')[3]).toBe(0.14)
    }
  })

  it('keeps white readable on both ends of the brand 500 steps (bold button text)', () => {
    expect(contrastRatio('#ffffff', theme['color-primary-500'] ?? '')).toBeGreaterThanOrEqual(WCAG_AA_LARGE)
    expect(contrastRatio('#ffffff', theme['color-secondary-500'] ?? '')).toBeGreaterThanOrEqual(WCAG_AA_LARGE)
  })

  it.each(['gradient-primary', 'gradient-primary-hover', 'gradient-primary-active'])('keeps the %s label at AA on both ends', (token) => {
    /** The colour stops of the gradient, each one a hex colour. */
    const stops = [...(theme[token] ?? '').matchAll(/#[0-9a-f]{6}/gi)].map((match) => match[0])
    expect(stops).toHaveLength(2)
    for (const stop of stops) {
      expect(contrastRatio(theme['text-on-primary'] ?? '', stop), `${token} ${stop}`).toBeGreaterThanOrEqual(WCAG_AA)
    }
  })

  it('draws the off track of a switch at 3:1 or more on every surface (the non-text minimum, the same ratio as large text)', () => {
    for (const [surfaceName, color] of Object.entries(surface)) {
      expect(contrastRatio(theme['control-track'] ?? '', color), `control-track on ${surfaceName}`).toBeGreaterThanOrEqual(WCAG_AA_LARGE)
    }
  })

  it.runIf(themeName === 'light')('draws the border of an interactive control at 3:1 or more on the card, the ground and the field', () => {
    for (const name of ['card', 'base', 'field']) {
      expect(contrastRatio(over(theme['border-control'] ?? '', surface[name] ?? ''), surface[name] ?? ''), `border-control on ${name}`).toBeGreaterThanOrEqual(WCAG_AA_LARGE)
      expect(contrastRatio(over(theme['border-control-hover'] ?? '', surface[name] ?? ''), surface[name] ?? ''), `border-control-hover on ${name}`).toBeGreaterThanOrEqual(WCAG_AA_LARGE)
    }
  })

  it('draws chart ticks that pass AA on the card', () => {
    expect(contrastRatio(theme['chart-tick'] ?? '', surface.card ?? '')).toBeGreaterThanOrEqual(WCAG_AA)
  })
})

describe('brand ramps', () => {
  it.each([['primary', '#4c81ff'], ['secondary', '#8b5cf6']])('%s is the OKLCH scale of its 500', (name, hex) => {
    /** The ramp the brand palette builds around the 500 step. */
    const scale = buildScale(hex)
    /** The steps main.css declares for this ramp. */
    const declared = SHADE_STEPS.filter((step) => DARK[`color-${name}-${step}`] !== undefined)
    expect(declared).toContain(500)
    for (const step of declared) {
      expect(DARK[`color-${name}-${step}`], `${name}-${step}`).toBe(rgbToHex(scale[step]))
    }
  })
})

describe('copies of a token', () => {
  it('keeps the chart fallback palette and tick colour equal to the dark chart tokens', () => {
    const theme = readChartTheme()
    expect(theme.palette).toEqual([1, 2, 3, 4, 5, 6].map((index) => DARK[`chart-${index}`]))
    expect(theme.grid).toBe(DARK['chart-grid'])
    expect(theme.tick).toBe(DARK['chart-tick'])
    expect(theme.barTop).toBe(DARK['chart-bar-top'])
    expect(theme.barBottom).toBe(DARK['chart-bar-bottom'])
    expect(theme.barDim).toBe(DARK['chart-bar-dim'])
    expect(theme.target).toBe(DARK['chart-target'])
    expect(theme.barGlow).toBe(DARK['chart-bar-glow'])
  })

  it('keeps the browser chrome colour equal to the ground of each theme', () => {
    expect(THEME_BOOT_CONFIG.colors.dark).toBe(DARK['surface-base'])
    expect(THEME_BOOT_CONFIG.colors.light).toBe(LIGHT['surface-base'])
    expect(INDEX_HTML).toContain(`media="(prefers-color-scheme: dark)" content="${DARK['surface-base']}"`)
    expect(INDEX_HTML).toContain(`media="(prefers-color-scheme: light)" content="${LIGHT['surface-base']}"`)
  })
})

describe('tokens the later steps rely on', () => {
  it.each([
    'ground-glow', 'glass-blur', 'gradient-primary', 'glow-primary', 'nav-active-bg', 'nav-active-border',
    'surface-active', 'gradient-bar', 'gradient-bar-warn', 'chart-bar-top', 'chart-bar-bottom', 'chart-bar-dim', 'chart-target', 'chart-1', 'chart-2', 'chart-3', 'chart-4', 'chart-5', 'chart-6',
  ])('declares %s in both themes', (token) => {
    expect(DARK[token]).toBeDefined()
    expect(LIGHT[token]).toBeDefined()
  })

  it('declares the four radii', () => {
    expect(THEME_STATIC['radius-control']).toBe('12px')
    expect(THEME_STATIC['radius-card']).toBe('20px')
    expect(THEME_STATIC['radius-panel']).toBe('22px')
    expect(THEME_STATIC['radius-pill']).toBe('999px')
  })

  it('draws the dark ground and the card as the concept does', () => {
    expect(DARK['ground-glow']).toBe('radial-gradient(900px 500px at 85% -10%, rgb(76 129 255 / 0.35), transparent 60%), radial-gradient(700px 500px at 10% 110%, rgb(139 92 246 / 0.25), transparent 60%)')
    expect(DARK['surface-base']).toBe('#070b18')
    expect(DARK['surface-card']).toBe('rgb(255 255 255 / 0.05)')
    expect(DARK['border']).toBe('rgb(255 255 255 / 0.09)')
    expect(DARK['text-muted']).toBe('#9aa6c4')
    expect(DARK['surface-hover']).toBe('rgb(255 255 255 / 0.08)')
    expect(DARK['surface-active']).toBe('rgb(255 255 255 / 0.12)')
  })

  it('draws the capacity bars and chart bars as the concept does', () => {
    expect(DARK['gradient-bar']).toBe('linear-gradient(90deg, #4c81ff, #8b5cf6)')
    expect(DARK['gradient-bar-warn']).toBe('linear-gradient(90deg, #f59e0b, #f87171)')
    expect(DARK['chart-bar-top']).toBe('#7ca0ff')
    expect(DARK['chart-bar-bottom']).toBe('#4c81ff')
    expect(DARK['chart-bar-dim']).toBe('rgb(124 160 255 / 0.28)')
    expect(DARK['chart-target']).toBe('rgb(154 166 196 / 0.6)')
  })

  it('overrides the primary gradient and its states in the light theme only', () => {
    for (const token of ['gradient-primary', 'gradient-primary-hover', 'gradient-primary-active']) {
      expect(tokensOf(':root.light')[token], token).toBeDefined()
      expect(tokensOf(':root.light')[token], token).not.toBe(tokensOf(':root')[token])
    }
  })

  it('draws the light theme as Daylight Glass: white cards with a slate border and a layered shadow', () => {
    expect(LIGHT['surface-base']).toBe('#f5f7fc')
    expect(LIGHT['surface-card']).toBe('#ffffff')
    expect(LIGHT['surface-sidebar']).toBe('#ffffff')
    expect(LIGHT['border']).toBe('#e4e8f2')
    expect(LIGHT['border-subtle']).toBe('#eef1f6')
    expect(LIGHT['border-strong']).toBe('#d9dfeb')
    expect(LIGHT['border-control']).toBe('#7f8aa1')
    expect(LIGHT['text-primary']).toBe('#0f172a')
    expect(LIGHT['text-secondary']).toBe('#475569')
    expect(LIGHT['nav-active-bg']).toBe('#eef3ff')
    expect(LIGHT['nav-active-border']).toBe('#c9d7ff')
    expect(LIGHT['chart-bar-dim']).toBe('#c9d6f8')
    expect(LIGHT['scrollbar-thumb']).toBe('#c5cddc')
    expect(LIGHT['overlay-backdrop']).toBe('rgb(15 23 42 / 0.35)')
    expect(LIGHT['elevation-card']).toBe('0 1px 2px rgb(15 23 42 / 0.04), 0 8px 24px rgb(15 23 42 / 0.06)')
  })

  it('paints the card shadow through the glass utility and only in the light theme', () => {
    expect(DARK['elevation-card']).toBe('none')
    expect(CSS).toMatch(/@utility glass \{[^}]*box-shadow: var\(--elevation-card\);/)
    expect(THEME_TOKENS['shadow-card']).toBe('var(--elevation-card)')
  })

  it('hides the browser spin arrows of a number field in every engine', () => {
    expect(CSS).toContain('input[type="number"]::-webkit-inner-spin-button')
    expect(CSS).toContain('input[type="number"] { -moz-appearance: textfield; appearance: textfield; }')
  })

  it('draws one thin scrollbar everywhere, with no arrows and no per-component variant', () => {
    // The standard properties stay behind the support check: in Chromium they would bring back the arrow buttons.
    expect(CSS).toContain('@supports not selector(::-webkit-scrollbar) {')
    expect(CSS).toContain('* { scrollbar-width: thin; scrollbar-color: var(--scrollbar-thumb) transparent; }')
    expect(CSS).toContain('::-webkit-scrollbar { width: 8px; height: 8px; }')
    expect(CSS).toContain('::-webkit-scrollbar-button { display: none; width: 0; height: 0; }')
    expect(CSS).toContain('::-webkit-scrollbar-thumb:hover { background: var(--scrollbar-thumb-hover); }')
    expect(CSS).not.toContain('@utility scroll-subtle')
    expect(DARK['scrollbar-thumb']).toBeDefined()
    expect(LIGHT['scrollbar-thumb-hover']).toBeDefined()
    expect(LIGHT['scrollbar-thumb']).not.toBe(DARK['scrollbar-thumb'])
  })

  it('does not widen every table cell with tabular figures (only numeric cells opt in)', () => {
    expect(CSS).not.toMatch(/table\s*\{[^}]*font-variant-numeric/)
  })

  it('declares the glass, glass-strong and ground utilities', () => {
    for (const name of ['glass', 'glass-strong', 'ground']) {
      expect(CSS).toContain(`@utility ${name} {`)
    }
  })

  it('falls back to solid surfaces without backdrop blur and under reduced transparency', () => {
    expect(CSS).toContain('@supports not ((backdrop-filter: blur(1px))')
    expect(CSS).toContain('@media (prefers-reduced-transparency: reduce)')
    expect(CSS.match(/--glass-bg:\s*var\(--surface-card-solid\)/g)).toHaveLength(2)
  })
})

describe('fonts', () => {
  it('self-hosts the display face and falls back to the Armenian face', () => {
    expect(CSS).toContain('@import "@fontsource-variable/space-grotesk";')
    expect(THEME_STATIC['font-display']).toMatch(/^'Space Grotesk Variable'.*'Noto Sans Armenian'/)
  })
})

/** The theme variables the utilities are generated from: the inline mappings and the static tokens. */
const THEME_TOKENS: Tokens = { ...tokensOf('@theme inline'), ...THEME_STATIC }

/** Every `.vue` and `.ts` file under `src/`, with its text, as the Tailwind scanner would read them. */
const SOURCES: ReadonlyArray<{ readonly file: string, readonly text: string }> = readdirSync(join(ROOT, 'src'), { recursive: true, encoding: 'utf8' })
  .filter((file) => /\.(vue|ts)$/.test(file))
  .map((file) => ({ file, text: readFileSync(join(ROOT, 'src', file), 'utf8') }))

/**
 * Collects the distinct matches of one pattern across the sources.
 *
 * @param pattern - A global pattern whose first group is the part to collect.
 * @returns The distinct captured values.
 */
const collect = (pattern: RegExp): string[] => {
  const found = new Set<string>()
  for (const { text } of SOURCES) {
    for (const match of text.matchAll(pattern)) {
      found.add(match[1] ?? '')
    }
  }
  return [...found]
}

describe('utilities used by the sources exist', () => {
  it('only names colours that @theme declares (a missing one generates no CSS and silently renders nothing)', () => {
    const used = collect(/(?<![\w-])(?:bg|text|border|ring|fill|stroke|from|via|to|outline|divide|accent)-((?:surface|text|border|status|tint|primary|secondary|bar)-(?:[a-z]+(?:-[a-z]+)?|\d+))(?![\w-])/g)
    expect(used.length).toBeGreaterThan(0)
    for (const name of used) {
      expect(THEME_TOKENS[`color-${name}`], `colour utility for "${name}"`).toBeDefined()
    }
  })

  it('does not use a palette that is not declared (warning, danger, success, info)', () => {
    expect(collect(/(?<![\w-])(?:bg|text|border|ring|fill|stroke)-((?:warning|danger|success|info)-\d+)/g)).toEqual([])
  })

  it('names only the radii that @theme declares', () => {
    const used = collect(/(?<![\w-])rounded(?:-[trbl]{1,2})?-(control|card|panel|pill)(?![\w-])/g)
    expect(used.length).toBeGreaterThan(0)
    for (const name of used) {
      expect(THEME_TOKENS[`radius-${name}`], `radius "${name}"`).toBeDefined()
    }
  })

  it('takes every duration from a declared token', () => {
    const used = collect(/duration-\(--duration-([a-z]+)\)/g)
    expect(used.length).toBeGreaterThan(0)
    for (const name of used) {
      expect(THEME_TOKENS[`duration-${name}`], `duration "${name}"`).toBeDefined()
    }
  })

  it('backs every brand typeface class with a font token', () => {
    const used = collect(/fontClass: 'font-(brand-[a-z-]+)'/g)
    expect(used.length).toBeGreaterThan(0)
    for (const name of used) {
      expect(THEME_TOKENS[`font-${name}`], `font "${name}"`).toBeDefined()
    }
  })

  it('declares every custom variant the sources use', () => {
    expect(CSS).toContain('@custom-variant light (&:where(.light, .light *));')
    expect(collect(/(?<![\w-])(light):[a-z]/g)).toEqual(['light'])
  })
})
