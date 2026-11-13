/**
 * Unit tests for the row key generator.
 */
import { describe, expect, it } from 'vitest'
import { nextRowKey } from '@/lib/rowKey'

describe('nextRowKey', () => {
  it('never returns the same key twice', () => {
    const keys = new Set(Array.from({ length: 50 }, () => nextRowKey()))

    expect(keys.size).toBe(50)
  })
})
