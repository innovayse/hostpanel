/**
 * Unit tests for the typography vocabulary: every set maps every member, `plain` adds nothing.
 */
import { describe, expect, it } from 'vitest'
import {
  headingVariantClasses,
  labelVariantClasses,
  textSizeClasses,
  textToneClasses,
  UiHeadingVariant,
  UiLabelVariant,
  UiTextSize,
  UiTextTone,
} from '@/utils/ui/text'

describe('typography maps', () => {
  it.each([
    ['tone', UiTextTone, textToneClasses],
    ['size', UiTextSize, textSizeClasses],
    ['heading', UiHeadingVariant, headingVariantClasses],
    ['label', UiLabelVariant, labelVariantClasses],
  ] as const)('the %s map is empty for plain and non-empty for every other member', (_name, set, map) => {
    for (const member of Object.values(set)) {
      const classes = (map as Record<string, string>)[member]
      if (member === 'plain') {
        expect(classes).toBe('')
      } else {
        expect(classes?.length).toBeGreaterThan(0)
      }
    }
  })

  it('draws a field label as a block caption and an inline label as a row', () => {
    expect(labelVariantClasses.field).toContain('block')
    expect(labelVariantClasses.inline).toContain('flex')
  })
})

describe('field label look', () => {
  it('is the concept caption: 13px, medium weight, in the muted text token (AA on both grounds)', () => {
    expect(labelVariantClasses.field).toContain('text-[0.8125rem]')
    expect(labelVariantClasses.field).toContain('font-medium')
    expect(labelVariantClasses.field).toContain('text-text-muted')
    expect(labelVariantClasses.field).not.toContain('uppercase')
  })
})
