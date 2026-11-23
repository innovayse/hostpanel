/**
 * Unit tests for the whole-surface link vocabulary: every look has classes, and only the looks that carry a surface
 * light up on hover.
 */
import { describe, expect, it } from 'vitest'
import { UiRowLinkLook, rowLinkLookClasses, rowLinkLooksWithHover } from '@/utils/ui/rowLink'

describe('UiRowLinkLook', () => {
  it('lists unique, non-empty values', () => {
    const values = Object.values(UiRowLinkLook)

    expect(values.length).toBeGreaterThan(0)
    expect(new Set(values).size).toBe(values.length)
    expect(values.every((value) => value.length > 0)).toBe(true)
  })
})

describe('rowLinkLookClasses', () => {
  it('has classes for every look', () => {
    expect(Object.keys(rowLinkLookClasses).sort()).toEqual(Object.values(UiRowLinkLook).sort())
    expect(Object.values(rowLinkLookClasses).every((classes) => classes.length > 0)).toBe(true)
  })
})

describe('rowLinkLooksWithHover', () => {
  it('lights up every look but the bare block', () => {
    expect([...rowLinkLooksWithHover].sort()).toEqual([UiRowLinkLook.Card, UiRowLinkLook.Divided, UiRowLinkLook.Tile].sort())
    expect(rowLinkLooksWithHover.has(UiRowLinkLook.Block)).toBe(false)
  })
})
