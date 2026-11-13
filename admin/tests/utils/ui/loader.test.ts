/**
 * Unit tests for the loading ring's sizes: every control size has a square diameter, as complete
 * classes Tailwind can emit.
 */
import { describe, expect, it } from 'vitest'
import { loaderSizeClasses } from '@/utils/ui/loader'
import { UiSize } from '@/utils/ui/size'

describe('loader sizes', () => {
  it('has a ring for every control size', () => {
    expect(Object.keys(loaderSizeClasses).sort()).toEqual(Object.values(UiSize).sort())
  })

  it('draws every ring as a square of literal classes', () => {
    for (const classes of Object.values(loaderSizeClasses)) {
      const [height = '', width = ''] = classes.split(' ')
      expect(height.replace('h-', '')).toBe(width.replace('w-', ''))
      expect(classes).not.toContain('$')
    }
  })

  it('grows with the size', () => {
    expect(loaderSizeClasses.sm).toBe('h-4 w-4')
    expect(loaderSizeClasses.md).toBe('h-5 w-5')
    expect(loaderSizeClasses.lg).toBe('h-6 w-6')
  })
})
