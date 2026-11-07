/**
 * Unit tests for the colour helpers: luminance of black and white, the readable text colour on dark,
 * light and mid colours, and the fallback for text that is not a hex colour.
 */
import { describe, expect, it } from 'vitest'
import { DARK_TEXT_COLOR, LIGHT_TEXT_COLOR, readableTextColor, relativeLuminance } from '@/utils/ui/color'

describe('relativeLuminance', () => {
  it('is 0 for black and 1 for white', () => {
    expect(relativeLuminance('#000000')).toBe(0)
    expect(relativeLuminance('#FFFFFF')).toBeCloseTo(1, 5)
  })

  it('is null for text that is not a six-digit hex colour', () => {
    expect(relativeLuminance('red')).toBeNull()
    expect(relativeLuminance('#fff')).toBeNull()
  })
})

describe('readableTextColor', () => {
  it('uses light text on a dark colour', () => {
    expect(readableTextColor('#123456')).toBe(LIGHT_TEXT_COLOR)
  })

  it('uses dark text on a light colour', () => {
    expect(readableTextColor('#facc15')).toBe(DARK_TEXT_COLOR)
    expect(readableTextColor('#ffffff')).toBe(DARK_TEXT_COLOR)
  })

  it('falls back to light text for an invalid colour', () => {
    expect(readableTextColor('nope')).toBe(LIGHT_TEXT_COLOR)
  })
})
