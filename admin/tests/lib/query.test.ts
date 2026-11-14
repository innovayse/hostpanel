/**
 * Tests for the address query helpers.
 */
import { describe, expect, it } from 'vitest'
import { readQueryString } from '@/lib/query'

describe('readQueryString', () => {
  it('returns a single value as it is', () => {
    expect(readQueryString('a')).toBe('a')
  })

  it('returns the first of a repeated key', () => {
    expect(readQueryString(['a', 'b'])).toBe('a')
  })

  it('returns an empty string for a missing key or a null value', () => {
    expect(readQueryString(undefined)).toBe('')
    expect(readQueryString(null)).toBe('')
    expect(readQueryString([])).toBe('')
  })
})
