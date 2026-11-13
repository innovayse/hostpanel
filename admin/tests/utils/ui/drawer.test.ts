/**
 * Unit tests for the drawer vocabulary.
 */
import { describe, expect, it } from 'vitest'
import { UI_SHEET_QUERY, UiDrawerSide } from '@/utils/ui/drawer'

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
