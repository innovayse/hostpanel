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
  iconButtonIconSize,
  iconSizeClasses,
  UiSize,
} from '@/utils/ui/size'

describe('control sizes', () => {
  it('keeps xs, sm, md and lg', () => {
    expect(Object.values(UiSize)).toEqual(['xs', 'sm', 'md', 'lg'])
  })

  it.each([
    ['field', fieldSizeClasses],
    ['textarea', textareaSizeClasses],
    ['button', buttonSizeClasses],
    ['link button', buttonLinkSizeClasses],
    ['control radius', controlRadiusClasses],
    ['icon button', iconButtonSizeClasses],
    ['icon', iconSizeClasses],
    ['icon in an icon button', iconButtonIconSize],
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

  it('makes xs a 20px, fully round control for the remove button of a chip and the clear button of a search field', () => {
    expect(iconButtonSizeClasses.xs).toBe('w-5 h-5')
    expect(controlRadiusClasses.xs).toBe('rounded-full')
    expect(iconSizeClasses.xs).toBe('w-3 h-3')
    expect(iconButtonSizeClasses.sm).toBe('w-8 h-8')
  })

  it('lets a multi-line field grow with its rows instead of fixing a height', () => {
    for (const size of Object.values(UiSize)) {
      expect(textareaSizeClasses[size]).not.toMatch(/(^| )h-/)
    }
  })
})
