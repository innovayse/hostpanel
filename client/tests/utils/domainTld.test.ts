/**
 * Unit tests for `utils/domainTld.ts`: the extension is everything after the first dot.
 *
 * @module tests/utils/domainTld.test
 */

import { describe, expect, it } from 'vitest'
import { tldOf } from '~/utils/domainTld'

describe('tldOf', () => {
  it('returns the extension without the dot', () => {
    expect(tldOf('example.com')).toBe('com')
  })

  it('keeps a multi-part extension whole', () => {
    expect(tldOf('example.co.uk')).toBe('co.uk')
  })

  it('returns an empty string when the dot is last', () => {
    expect(tldOf('example.')).toBe('')
  })
})
