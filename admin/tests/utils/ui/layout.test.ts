/**
 * Unit tests for the layout vocabulary: each closed set maps to literal classes.
 */
import { describe, expect, it } from 'vitest'
import {
  alignItemsClasses,
  gapClasses,
  containerGridColumnClasses,
  gridColumnClasses,
  justifyClasses,
  pairedGridColumnClasses,
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

  it('counts columns by the container width, one column below 32rem', () => {
    expect(Object.keys(containerGridColumnClasses)).toEqual(Object.keys(gridColumnClasses))
    expect(containerGridColumnClasses[2]).toBe('grid-cols-1 @lg:grid-cols-2')
    expect(containerGridColumnClasses[4]).toBe('grid-cols-1 @lg:grid-cols-2 @4xl:grid-cols-4')
  })

  it('keeps two columns on a phone for a grid of small tiles', () => {
    expect(Object.keys(pairedGridColumnClasses)).toEqual(Object.keys(gridColumnClasses))
    expect(pairedGridColumnClasses[4]).toBe('grid-cols-2 lg:grid-cols-4')
    expect(pairedGridColumnClasses[1]).toBe('grid-cols-1')
  })

  it('covers every split ratio with a single column on narrow screens', () => {
    for (const ratio of Object.values(UiSplitRatio)) {
      expect(splitRatioClasses[ratio]).toMatch(/^grid-cols-1 lg:/)
    }
  })
})
