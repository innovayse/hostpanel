/**
 * Unit tests for `Key`: the values are the strings the browser reports in `KeyboardEvent.key`.
 */
import { describe, expect, it } from 'vitest'
import { Key } from '@/utils/ui/keys'

describe('Key', () => {
  it('holds the browser key names', () => {
    expect(Object.values(Key)).toEqual(['Enter', 'Escape', 'Tab', 'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End'])
  })
})
