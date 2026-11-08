/**
 * Contrast tests for the colours the documentation theme sets in `custom.css`: link and accent text and
 * code comments must reach 4.5:1, the WCAG AA ratio for normal text, in the theme they are used in.
 */
import { readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

/** The WCAG AA ratio for normal text. */
const MIN_RATIO = 4.5

/** The stylesheet under test. */
const css = readFileSync(resolve(dirname(fileURLToPath(import.meta.url)), '../.vitepress/theme/custom.css'), 'utf8')

/**
 * Reads the value of a custom property from a rule of the stylesheet.
 * @param selector The selector of the rule, exactly as written.
 * @param name The custom property, such as `--vp-c-brand-1`.
 * @returns The colour written for it, as `#rrggbb`.
 */
const colourIn = (selector: string, name: string): string => {
  const start = css.indexOf(`${selector} {`)
  expect(start, `rule ${selector}`).toBeGreaterThanOrEqual(0)
  const open = css.indexOf('{', start)
  const rule = css.slice(open, css.indexOf('}', open))
  const match = new RegExp(`${name}:\\s*(#[0-9a-fA-F]{6})`).exec(rule)
  expect(match, `${name} in ${selector}`).not.toBeNull()
  return (match as RegExpExecArray)[1]
}

/**
 * Relative luminance of a colour, as WCAG defines it.
 * @param hex The colour as `#rrggbb`.
 * @returns A number from 0 (black) to 1 (white).
 */
const luminance = (hex: string): number => {
  const [r, g, b] = [1, 3, 5].map((i) => {
    const channel = parseInt(hex.slice(i, i + 2), 16) / 255
    return channel <= 0.03928 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4
  })
  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}

/**
 * Contrast ratio of two colours.
 * @param a One colour as `#rrggbb`.
 * @param b The other colour as `#rrggbb`.
 * @returns The ratio, from 1 to 21.
 */
const ratio = (a: string, b: string): number => {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x)
  return (hi + 0.05) / (lo + 0.05)
}

describe('theme contrast', () => {
  it('keeps light link colours at 4.5:1 on white', () => {
    expect(ratio(colourIn(':root', '--vp-c-brand-1'), '#ffffff')).toBeGreaterThanOrEqual(MIN_RATIO)
    expect(ratio(colourIn(':root', '--vp-c-brand-2'), '#ffffff')).toBeGreaterThanOrEqual(MIN_RATIO)
  })

  it('keeps dark link colours at 4.5:1 on the dark background', () => {
    expect(ratio(colourIn('.dark', '--vp-c-brand-1'), '#1b1b1f')).toBeGreaterThanOrEqual(MIN_RATIO)
  })

  it('keeps light code comments at 4.5:1 on the light code background', () => {
    const colour = colourIn('.vp-code span[style*="--shiki-light:#6A737D"]', '--shiki-light')
    expect(ratio(colour, '#f6f6f7')).toBeGreaterThanOrEqual(MIN_RATIO)
  })

  it('keeps dark code comments at 4.5:1 on the dark code background', () => {
    const colour = colourIn('.dark .vp-code span[style*="--shiki-dark:#6A737D"]', '--shiki-dark')
    expect(ratio(colour, '#161618')).toBeGreaterThanOrEqual(MIN_RATIO)
  })
})
