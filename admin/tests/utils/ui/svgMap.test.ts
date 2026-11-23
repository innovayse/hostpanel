/**
 * Unit tests for the map and legend vocabulary: the swatch shapes are a closed set and each has classes.
 */
import { describe, expect, it } from 'vitest'
import { UiLegendSwatchShape, legendSwatchClasses } from '@/utils/ui/svgMap'

describe('UiLegendSwatchShape', () => {
  it('lists unique, non-empty values', () => {
    const values = Object.values(UiLegendSwatchShape)

    expect(values.length).toBeGreaterThan(0)
    expect(new Set(values).size).toBe(values.length)
    expect(values.every((value) => value.length > 0)).toBe(true)
  })
})

describe('legendSwatchClasses', () => {
  it('has classes for every swatch shape', () => {
    expect(Object.keys(legendSwatchClasses).sort()).toEqual(Object.values(UiLegendSwatchShape).sort())
    expect(Object.values(legendSwatchClasses).every((classes) => classes.length > 0)).toBe(true)
  })
})
