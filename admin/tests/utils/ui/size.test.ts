/**
 * Unit tests for the control sizes: every class map covers every size.
 */
import { describe, expect, it } from 'vitest'
import {
  buttonLinkSizeClasses,
  buttonSizeClasses,
  controlRadiusClasses,
  fieldSizeClasses,
  textareaSizeClasses,
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
    ['textarea', textareaSizeClasses],
    ['button', buttonSizeClasses],
    ['link button', buttonLinkSizeClasses],
    ['control radius', controlRadiusClasses],
    ['icon button', iconButtonSizeClasses],
    ['icon', iconSizeClasses],
  ])('the %s map has non-empty classes for every size', (_name, map) => {
    for (const size of Object.values(UiSize)) {
      expect(map[size].length).toBeGreaterThan(0)
    }
  })

  it('shares the concept height scale (32, 42, 48 px) between fields and buttons', () => {
    expect(fieldSizeClasses.md).toContain('h-10.5')
    expect(buttonSizeClasses.md).toContain('h-10.5')
    expect(fieldSizeClasses.sm).toContain('h-8')
    expect(buttonSizeClasses.lg).toContain('h-12')
  })

  it('lets a multi-line field grow with its rows instead of fixing a height', () => {
    for (const size of Object.values(UiSize)) {
      expect(textareaSizeClasses[size]).not.toMatch(/(^| )h-/)
    }
  })
})
