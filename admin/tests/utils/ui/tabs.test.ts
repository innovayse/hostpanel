/**
 * Unit tests for the arrow-key navigation shared by the tab strip and the segmented toggle.
 */
import { describe, expect, it } from 'vitest'
import { neighbourValue } from '@/utils/ui/tabs'

/** Three items, the middle one disabled. */
const items = [
  { value: 'a', label: 'A' },
  { value: 'b', label: 'B', disabled: true },
  { value: 'c', label: 'C' },
]

describe('neighbourValue', () => {
  it('skips a disabled item', () => {
    expect(neighbourValue(items, 0, 'ArrowRight')).toBe('c')
    expect(neighbourValue(items, 2, 'ArrowLeft')).toBe('a')
  })

  it('wraps at both ends', () => {
    expect(neighbourValue(items, 2, 'ArrowRight')).toBe('a')
    expect(neighbourValue(items, 0, 'ArrowLeft')).toBe('c')
  })

  it('does not move on any other key', () => {
    expect(neighbourValue(items, 0, 'Enter')).toBeUndefined()
  })
})
