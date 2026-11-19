/**
 * Unit tests for the drawer vocabulary.
 */
import { describe, expect, it } from 'vitest'
import { UiDrawerPresentation, UiDrawerSide } from '@/utils/ui/drawer'

describe('UiDrawerSide', () => {
  it('lists unique, non-empty values', () => {
    const values = Object.values(UiDrawerSide)

    expect(values.length).toBeGreaterThan(0)
    expect(new Set(values).size).toBe(values.length)
    expect(values.every((value) => value.length > 0)).toBe(true)
  })
})

describe('UiDrawerPresentation', () => {
  it('is the closed pair of a side panel and a bottom sheet, with unique non-empty values', () => {
    const values = Object.values(UiDrawerPresentation)

    expect(values).toEqual(['side', 'sheet'])
    expect(new Set(values).size).toBe(values.length)
  })
})
