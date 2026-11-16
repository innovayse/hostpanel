/**
 * Tests for `tests/helpers/documentHead.ts`.
 *
 * @module tests/helpers/documentHead.test
 */

import { describe, expect, it, vi } from 'vitest'
import { documentHead } from './documentHead'

describe('documentHead', () => {
  it('returns the call that carries htmlAttrs, whatever its position among the calls', () => {
    const useHead = vi.fn()
    useHead(() => ({ script: [] }))
    useHead({ link: [] })
    useHead({ htmlAttrs: { lang: 'en' } })

    expect(documentHead(useHead)).toEqual({ htmlAttrs: { lang: 'en' } })
  })

  it('throws when no call carried htmlAttrs', () => {
    const useHead = vi.fn()
    useHead({ link: [] })

    expect(() => documentHead(useHead)).toThrow('useHead was never called with htmlAttrs.')
  })
})
