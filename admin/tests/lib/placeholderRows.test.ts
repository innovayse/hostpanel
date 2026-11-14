/**
 * Unit tests for placeholderRows: placeholders while the first read is pending, the rows themselves afterwards.
 */
import { describe, expect, it } from 'vitest'
import { placeholderRows } from '@/lib/placeholderRows'

describe('placeholderRows', () => {
  it('answers the requested number of nulls while loading, whatever rows are held', () => {
    expect(placeholderRows(['a', 'b'], true, 3)).toEqual([null, null, null])
    expect(placeholderRows<string>([], true, 2)).toEqual([null, null])
  })

  it('answers the rows themselves once loaded', () => {
    const rows = ['a', 'b']

    expect(placeholderRows(rows, false, 3)).toBe(rows)
  })

  it('answers an empty list when loaded with no rows, so the empty state can show', () => {
    expect(placeholderRows<string>([], false, 3)).toEqual([])
  })

  it('answers no placeholders for a count of zero', () => {
    expect(placeholderRows(['a'], true, 0)).toEqual([])
  })
})
