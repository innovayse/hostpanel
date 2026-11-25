/**
 * Tests for `fitChipCount`: every chip when all fit, the leading chips that leave room for the "+N" chip otherwise,
 * and none when not even one fits beside it.
 */
import { describe, expect, it } from 'vitest'
import { fitChipCount } from '@/utils/ui/chipRow'

describe('fitChipCount', () => {
  it('keeps every chip when they all fit, without room for a "+N" chip', () => {
    expect(fitChipCount([40, 50, 30], 24, 130, 4)).toBe(3)
  })

  it('keeps the leading chips that fit with the gaps and the "+N" chip', () => {
    expect(fitChipCount([40, 50, 30], 24, 122, 4)).toBe(2)
    expect(fitChipCount([40, 50, 30], 24, 100, 4)).toBe(1)
  })

  it('keeps none when not even the first chip fits beside the "+N" chip', () => {
    expect(fitChipCount([90, 50], 24, 100, 4)).toBe(0)
  })

  it('keeps nothing for no chips', () => {
    expect(fitChipCount([], 24, 0, 4)).toBe(0)
  })
})
