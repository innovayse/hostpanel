/**
 * Unit tests for the control sizes: every class map covers every size.
 */
import { describe, expect, it } from 'vitest'
import {
  buttonLinkSizeClasses,
  buttonSizeClasses,
  fieldSizeClasses,
  iconButtonSizeClasses,
  iconSizeClasses,
  UiSize,
} from '@/utils/ui/size'

describe('control sizes', () => {
  it('keeps sm, md and lg', () => {
    expect(Object.values(UiSize)).toEqual(['sm', 'md', 'lg'])
  })

  it.each([
    ['field', fieldSizeClasses],
    ['button', buttonSizeClasses],
    ['link button', buttonLinkSizeClasses],
    ['icon button', iconButtonSizeClasses],
    ['icon', iconSizeClasses],
  ])('the %s map has non-empty classes for every size', (_name, map) => {
    for (const size of Object.values(UiSize)) {
      expect(map[size].length).toBeGreaterThan(0)
    }
  })
})
