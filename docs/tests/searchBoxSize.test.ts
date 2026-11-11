/**
 * Unit tests for the sizing of the search modal (`LocalSearchBox.vue`): the compact command palette
 * dimensions, the phone layout and the reduced motion rule are pinned in the component's styles.
 */
import { readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

/** Source of the search modal component. */
const source = readFileSync(
  resolve(dirname(fileURLToPath(import.meta.url)), '../.vitepress/theme/components/LocalSearchBox.vue'),
  'utf-8',
).replace(/\r\n/g, '\n')

/**
 * Returns the declarations of the first rule whose selector is exactly `selector`.
 * @param selector - The selector to find, for example `.shell`.
 * @returns The text between the braces of that rule.
 */
const rule = (selector: string): string => {
  const start = source.indexOf(`\n${selector} {`)
  expect(start).toBeGreaterThan(-1)
  return source.slice(start, source.indexOf('}', start))
}

describe('search modal sizing', () => {
  it('is a 640px panel placed 12vh from the top', () => {
    const shell = rule('.shell')
    expect(shell).toContain('width: min(640px, 100vw - 24px)')
    expect(shell).toContain('margin: 12vh auto 0')
    expect(shell).toContain('border-radius: 22px')
  })

  it('has a 58px input row with 17px text', () => {
    expect(rule('.search-bar')).toContain('height: 58px')
    expect(rule('.search-input')).toContain('font-size: 17px')
  })

  it('limits the list height and pads it by 8px', () => {
    const results = rule('.results')
    expect(results).toContain('max-height: min(56vh, 460px)')
    expect(results).toContain('padding: 8px')
  })

  it('uses compact items, icon tiles and text', () => {
    expect(rule('.result.home-result')).toContain('min-height: 52px')
    expect(rule('.result.home-result')).toContain('padding: 8px 10px')
    expect(rule('.home-icon')).toContain('width: 36px')
    expect(rule('.home-icon')).toContain('border-radius: 10px')
    expect(rule('.home-title')).toContain('font-size: 14px')
    expect(rule('.home-title')).toContain('font-weight: 600')
    expect(rule('.home-crumb')).toContain('font-size: 12px')
    expect(rule('.home-chevron')).toContain('font-size: 18px')
  })

  it('styles group titles as small uppercase muted labels', () => {
    const title = rule('.group-title')
    expect(title).toContain('font-size: 11px')
    expect(title).toContain('text-transform: uppercase')
    expect(title).toContain('letter-spacing')
  })

  it('tints the highlighted item without a brand outline', () => {
    const selected = rule('.result.home-result.selected')
    expect(selected).toContain('background: var(--vp-c-brand-soft)')
    expect(selected).toContain('border-color: transparent')
  })

  it('keeps 10px margins on phones and honours reduced motion', () => {
    expect(source).toMatch(/@media \(max-width: 767px\) \{\n {2}\.shell \{\n {4}margin: 10px;/)
    expect(source).toContain('@media (prefers-reduced-motion: reduce)')
    expect(source).toMatch(/ {2}\.results \{\n {4}flex: 1;\n {4}max-height: none;/)
  })

  it('draws the footer hints at 12px with small key caps', () => {
    expect(rule('.search-keyboard-shortcuts')).toContain('font-size: 12px')
    expect(rule('.search-keyboard-shortcuts kbd')).toContain('font-size: 11px')
  })
})
