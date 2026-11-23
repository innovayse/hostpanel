/**
 * Unit tests for `utils/ui/enums.ts`: the kit's closed value sets keep their wire values.
 *
 * @module tests/utils/ui/enums.test
 */

import { describe, expect, it } from 'vitest'
import * as kit from '~/utils/ui/enums'

describe('Ui enums', () => {
  it('keeps the three-step size scale', () => {
    expect(Object.values(kit.UiSize)).toEqual(['sm', 'md', 'lg'])
  })

  it('keeps the button variants a screen may write as a literal', () => {
    expect(Object.values(kit.UiButtonVariant)).toEqual(['primary', 'secondary', 'outline', 'ghost', 'subtle', 'danger', 'bare'])
  })

  it('keeps the heading levels numeric, one to six', () => {
    expect(kit.UiHeadingLevel.H1).toBe(1)
    expect(kit.UiHeadingLevel.H6).toBe(6)
  })

  it('keeps the rich-text node kinds', () => {
    expect(Object.values(kit.UiRichNodeType)).toEqual(['text', 'element'])
  })

  it('keeps the tags each layout component may be drawn as', () => {
    expect(Object.values(kit.UiStackTag)).toEqual(['div', 'span', 'section', 'article', 'aside', 'nav', 'main', 'header', 'footer', 'figure', 'figcaption', 'ul', 'ol', 'li'])
    expect(Object.values(kit.UiGridTag)).toEqual(['div', 'ul', 'ol', 'dl'])
    expect(Object.values(kit.UiDecorTag)).toEqual(['div', 'span'])
    expect(Object.values(kit.UiGradientTextTag)).toEqual(['span', 'div', 'p', 'h1', 'h2', 'h3'])
    expect(Object.values(kit.UiAccentTag)).toEqual(['div', 'span', 'li', 'section', 'article'])
    expect(Object.values(kit.UiRichTextTag)).toEqual(['div', 'section', 'article'])
  })

  it('keeps the text tags, member names matching their values', () => {
    for (const [member, value] of Object.entries(kit.UiTextTag)) {
      expect(member.toLowerCase()).toBe(value)
    }
    expect(kit.UiTextTag.P).toBe('p')
  })

  it('has no duplicated value inside any string enum', () => {
    for (const [name, members] of Object.entries(kit)) {
      const values = Object.values(members as Record<string, string | number>).filter(value => typeof value === 'string')
      expect(new Set(values).size, name).toBe(values.length)
    }
  })
})
