/**
 * Tests for the brand palette. The numbers here are shared with `BrandPaletteTests.cs` on
 * the backend; a change on one side must be made on the other.
 *
 * @module utils/brandPalette.test
 */

import { describe, expect, it } from 'vitest'
import {
  SHADE_STEPS,
  bestTextOn,
  buildScale,
  contrastRatio,
  hexToRgb,
  luminance,
  rgbToHex,
  scaleToDeclarations,
} from './brandPalette'

describe('hexToRgb / rgbToHex', () => {
  it('round-trips and ignores case', () => {
    expect(hexToRgb('#0EA5E9')).toEqual([14, 165, 233])
    expect(rgbToHex([14, 165, 233])).toBe('#0ea5e9')
  })

  it('rejects anything that is not six hex digits with a hash', () => {
    expect(hexToRgb('0ea5e9')).toBeNull()
    expect(hexToRgb('#fff')).toBeNull()
    expect(hexToRgb('blue')).toBeNull()
  })
})

describe('buildScale', () => {
  it("pins the picked colour at 500 — bg-primary-500 is exactly the operator's choice", () => {
    expect(buildScale('#0ea5e9')[500]).toEqual([14, 165, 233])
    expect(buildScale('#1a73e8')[500]).toEqual([26, 115, 232])
  })

  it('is strictly monotonic in lightness from 50 down to 950 for any usable hue', () => {
    for (const hex of ['#0ea5e9', '#1a73e8', '#ffd600', '#e8710a', '#a855f7', '#22c55e', '#7de3ff', '#4b35b8']) {
      const scale = buildScale(hex)
      const ls = SHADE_STEPS.map(s => luminance(scale[s]))
      for (let i = 1; i < ls.length; i++) {
        expect(ls[i]!, `${hex} step ${SHADE_STEPS[i]} should be darker than ${SHADE_STEPS[i - 1]}`).toBeLessThan(ls[i - 1]!)
      }
    }
  })

  it('never inverts even for a near-black or near-white pick, where 8-bit rounding merges neighbours', () => {
    for (const hex of ['#111111', '#f5f5f5']) {
      const scale = buildScale(hex)
      const ls = SHADE_STEPS.map(s => luminance(scale[s]))
      for (let i = 1; i < ls.length; i++) {
        expect(ls[i]!).toBeLessThanOrEqual(ls[i - 1]!)
      }
      expect(ls[0]!).toBeGreaterThan(ls[ls.length - 1]!)
    }
  })

  it('ends near white at 50 and near black at 950', () => {
    for (const hex of ['#0ea5e9', '#ffd600', '#a855f7']) {
      const scale = buildScale(hex)
      expect(luminance(scale[50])).toBeGreaterThan(0.8)
      expect(luminance(scale[950])).toBeLessThan(0.05)
    }
  })

  it('keeps the hue: a yellow does not turn brown at 800', () => {
    const [r, g, b] = buildScale('#ffd600')[800]
    // Yellow-ish: red and green stay well above blue.
    expect(r).toBeGreaterThan(b + 40)
    expect(g).toBeGreaterThan(b + 30)
  })

  it('produces the reference shades shared with BrandPaletteTests.cs', () => {
    expect(rgbToHex(buildScale('#0ea5e9')[50])).toBe('#f5fbff')
    expect(rgbToHex(buildScale('#0ea5e9')[700])).toBe('#145e84')
    expect(rgbToHex(buildScale('#0ea5e9')[950])).toBe('#000d19')
    expect(rgbToHex(buildScale('#1a73e8')[700])).toBe('#154485')
    expect(rgbToHex(buildScale('#ffd600')[700])).toBe('#8d7710')
    expect(rgbToHex(buildScale('#a855f7')[200])).toBe('#e6d5ff')
  })

  it('refuses a non-colour', () => {
    expect(() => buildScale('blue')).toThrow(RangeError)
  })
})

describe('contrastRatio / bestTextOn', () => {
  it('matches the WCAG reference numbers', () => {
    expect(contrastRatio('#ffffff', '#000000')).toBeCloseTo(21, 5)
    expect(contrastRatio('#ffffff', '#0ea5e9')).toBeCloseTo(2.78, 1)
    expect(contrastRatio('#0ea5e9', '#ffffff')).toBeCloseTo(2.78, 1)
    expect(contrastRatio('#ffffff', '#1a73e8')).toBeCloseTo(4.5, 0)
  })

  it('puts white on blues and dark text on yellows', () => {
    expect(bestTextOn('#1a73e8')).toBe('#ffffff')
    expect(bestTextOn('#a855f7')).toBe('#ffffff')
    expect(bestTextOn('#ffd600')).toBe('#111111')
    expect(bestTextOn('#7de3ff')).toBe('#111111')
  })

  it('is NaN for a non-colour rather than throwing in a template', () => {
    expect(contrastRatio('blue', '#ffffff')).toBeNaN()
  })
})

describe('scaleToDeclarations', () => {
  it('emits space-separated channels for every step, ready for rgb(var(--x) / <alpha>)', () => {
    const css = scaleToDeclarations('brand-primary', buildScale('#0ea5e9'))
    expect(css).toContain('--brand-primary-500:14 165 233')
    expect(css.split(';')).toHaveLength(SHADE_STEPS.length)
  })
})
