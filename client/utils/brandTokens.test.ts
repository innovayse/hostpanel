/**
 * Tests for the `:root` rule the storefront emits for the brand.
 *
 * @module utils/brandTokens.test
 */

import { describe, expect, it } from 'vitest'
import { brandTokensCss } from './brandTokens'
import { fontStack, FONT_STACKS, BRAND_FONTS } from './brandFonts'

const none = { primary: '', accent: '', fontHeading: '', fontBody: '' }

describe('brandTokensCss', () => {
  it('emits nothing when nothing is set — the stylesheets\' fallbacks then apply', () => {
    expect(brandTokensCss(none)).toBe('')
  })

  it('emits the primary scale, both on-colours and no accent when only primary is set', () => {
    const css = brandTokensCss({ ...none, primary: '#1a73e8' })
    expect(css.startsWith(':root{')).toBe(true)
    expect(css).toContain('--brand-primary-500:26 115 232')
    expect(css).toContain('--brand-primary-50:')
    expect(css).toContain('--brand-primary-950:')
    expect(css).toContain('--brand-on-primary:255 255 255')
    expect(css).toContain('--brand-on-tint:')
    expect(css).not.toContain('--brand-accent')
    expect(css).not.toContain('--font-')
  })

  it('puts dark text on a yellow brand', () => {
    const css = brandTokensCss({ ...none, primary: '#ffd600' })
    expect(css).toContain('--brand-on-primary:17 17 17')
    expect(css).toContain('--brand-on-tint:17 17 17')
  })

  it('emits the accent scale independently', () => {
    const css = brandTokensCss({ ...none, accent: '#E8710A' })
    expect(css).toContain('--brand-accent-500:232 113 10')
    expect(css).toContain('--brand-on-accent:')
    expect(css).toContain('--brand-on-accent-tint:')
    expect(css).not.toContain('--brand-primary')
  })

  it('ignores a colour that does not parse instead of failing the page', () => {
    expect(brandTokensCss({ ...none, primary: 'blue' })).toBe('')
  })

  it('emits font stacks for known faces and nothing for unknown ones', () => {
    const css = brandTokensCss({ ...none, fontHeading: 'noto-serif-armenian', fontBody: 'comic-sans' })
    expect(css).toContain(`--font-heading:${FONT_STACKS['noto-serif-armenian']}`)
    expect(css).not.toContain('--font-body')
  })
})

describe('brandFonts', () => {
  it('every stack ends in the Armenian fallback', () => {
    for (const font of BRAND_FONTS) {
      expect(FONT_STACKS[font]).toContain('Noto Sans Armenian')
    }
  })

  it('resolves only the four known names', () => {
    expect(fontStack('inter')).toContain('Inter')
    expect(fontStack('system')).toContain('system-ui')
    expect(fontStack('')).toBe('')
    expect(fontStack(undefined)).toBe('')
    expect(fontStack('Inter')).toBe('')
  })
})
