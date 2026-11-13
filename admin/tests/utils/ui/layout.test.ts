/**
 * Unit tests for the layout vocabulary: each closed set maps to literal classes.
 */
import { describe, expect, it } from 'vitest'
import {
  alignItemsClasses,
  gapClasses,
  gridColumnClasses,
  justifyClasses,
  splitRatioClasses,
  UiAlignItems,
  UiGap,
  UiJustify,
  UiSplitRatio,
} from '@/utils/ui/layout'

describe('layout maps', () => {
  it('covers every gap with a gap- class', () => {
    for (const gap of Object.values(UiGap)) {
      expect(gapClasses[gap]).toMatch(/^gap-/)
    }
  })

  it('publishes every gap as --stack-gap, which the page header subtracts from its margin', () => {
    for (const gap of Object.values(UiGap)) {
      expect(gapClasses[gap]).toContain('[--stack-gap:')
    }
  })

  it('covers every alignment with an items- class', () => {
    for (const align of Object.values(UiAlignItems)) {
      expect(alignItemsClasses[align]).toBe(`items-${align}`)
    }
  })

  it('covers every distribution with a justify- class', () => {
    for (const justify of Object.values(UiJustify)) {
      expect(justifyClasses[justify]).toBe(`justify-${justify}`)
    }
  })

  it('offers 1, 2, 3, 4 and 6 columns, widening from narrow screens', () => {
    expect(Object.keys(gridColumnClasses)).toEqual(['1', '2', '3', '4', '6'])
    expect(gridColumnClasses[1]).toBe('grid-cols-1')
    expect(gridColumnClasses[3]).toBe('grid-cols-1 md:grid-cols-2 lg:grid-cols-3')
    expect(gridColumnClasses[6]).toContain('lg:grid-cols-6')
  })

  it('covers every split ratio with a single column on narrow screens', () => {
    for (const ratio of Object.values(UiSplitRatio)) {
      expect(splitRatioClasses[ratio]).toMatch(/^grid-cols-1 lg:/)
    }
  })
})
