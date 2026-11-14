/**
 * Unit tests for the drawer vocabulary.
 */
import { describe, expect, it } from 'vitest'
import { sheetQueries, UI_SHEET_LG_QUERY, UI_SHEET_QUERY, UiDrawerSheetBelow, UiDrawerSide } from '@/utils/ui/drawer'

describe('UiDrawerSide', () => {
  it('lists unique, non-empty values', () => {
    const values = Object.values(UiDrawerSide)

    expect(values.length).toBeGreaterThan(0)
    expect(new Set(values).size).toBe(values.length)
    expect(values.every((value) => value.length > 0)).toBe(true)
  })
})

describe('UI_SHEET_QUERY', () => {
  it('is the max-width query just under the sm breakpoint', () => {
    expect(UI_SHEET_QUERY).toBe('(max-width: 639.98px)')
  })
})

describe('sheetQueries', () => {
  it('maps each sheet breakpoint to the width of its Tailwind max- variant', () => {
    expect(UI_SHEET_LG_QUERY).toBe('(max-width: 1023.98px)')
    expect(sheetQueries[UiDrawerSheetBelow.Sm]).toBe(UI_SHEET_QUERY)
    expect(sheetQueries[UiDrawerSheetBelow.Lg]).toBe(UI_SHEET_LG_QUERY)
  })
})
