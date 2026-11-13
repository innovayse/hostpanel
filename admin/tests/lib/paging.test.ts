/**
 * Tests for the page arithmetic: page count, in-memory slice, the page window and the first and last row numbers.
 */
import { describe, expect, it } from 'vitest'
import { pageCount, pageEntryKey, pageFirstRow, pageLastRow, pageSlice, pageWindow } from '@/lib/paging'

describe('pageCount', () => {
  it('rounds up to whole pages', () => {
    expect(pageCount(41, 20)).toBe(3)
    expect(pageCount(40, 20)).toBe(2)
  })

  it('keeps one page for an empty list', () => {
    expect(pageCount(0, 20)).toBe(1)
  })
})

describe('pageSlice', () => {
  const items = [1, 2, 3, 4, 5]

  it('cuts the rows of the page', () => {
    expect(pageSlice(items, 1, 2)).toEqual([1, 2])
    expect(pageSlice(items, 2, 2)).toEqual([3, 4])
  })

  it('returns the short last page and nothing past it', () => {
    expect(pageSlice(items, 3, 2)).toEqual([5])
    expect(pageSlice(items, 4, 2)).toEqual([])
  })
})

describe('page row numbers', () => {
  it('numbers the first and last row of a page', () => {
    expect(pageFirstRow(1, 20)).toBe(1)
    expect(pageFirstRow(3, 20)).toBe(41)
    expect(pageLastRow(1, 20, 55)).toBe(20)
  })

  it('stops the last row at the total', () => {
    expect(pageLastRow(3, 20, 55)).toBe(55)
  })
})

describe('pageWindow', () => {
  it('is just page 1 for a single page', () => {
    expect(pageWindow(1, 1)).toEqual([1])
  })

  it('shows every page of a short list, filling a gap of one page', () => {
    expect(pageWindow(1, 7)).toEqual([1, 2, null, 7])
    expect(pageWindow(4, 7)).toEqual([1, 2, 3, 4, 5, 6, 7])
  })

  it('keeps the first and last page and gaps the long runs of a long list', () => {
    expect(pageWindow(1, 100)).toEqual([1, 2, null, 100])
    expect(pageWindow(50, 100)).toEqual([1, null, 49, 50, 51, null, 100])
    expect(pageWindow(100, 100)).toEqual([1, null, 99, 100])
  })

  it('has no gap next to the first or last page when the window touches it', () => {
    expect(pageWindow(3, 100)).toEqual([1, 2, 3, 4, null, 100])
    expect(pageWindow(98, 100)).toEqual([1, null, 97, 98, 99, 100])
  })
})

describe('pageEntryKey', () => {
  it('keys a page by its number and each gap by the side it is on', () => {
    const entries = pageWindow(50, 100)

    expect(entries.map((_, index) => pageEntryKey(entries, index, 50))).toEqual(['1', 'gap-start', '49', '50', '51', 'gap-end', '100'])
  })

  it('names a lone gap by its side of the current page', () => {
    const entries = pageWindow(1, 100)

    expect(entries.map((_, index) => pageEntryKey(entries, index, 1))).toEqual(['1', '2', 'gap-end', '100'])
  })
})
