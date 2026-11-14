/**
 * Unit tests for the drawer vocabulary.
 */
import { describe, expect, it } from 'vitest'
import { UI_BELOW_LG_QUERY, UI_BELOW_SM_QUERY } from '@/utils/ui/breakpoints'
import { sheetQueries, UiDrawerSheetBelow, UiDrawerSide } from '@/utils/ui/drawer'

describe('UiDrawerSide', () => {
  it('lists unique, non-empty values', () => {
    const values = Object.values(UiDrawerSide)

    expect(values.length).toBeGreaterThan(0)
    expect(new Set(values).size).toBe(values.length)
    expect(values.every((value) => value.length > 0)).toBe(true)
  })
})

describe('sheetQueries', () => {
  it('maps each sheet breakpoint to the width of its Tailwind max- variant', () => {
    expect(sheetQueries[UiDrawerSheetBelow.Sm]).toBe(UI_BELOW_SM_QUERY)
    expect(sheetQueries[UiDrawerSheetBelow.Lg]).toBe(UI_BELOW_LG_QUERY)
  })
})
