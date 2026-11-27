/**
 * Tests for the link finder: an element inside a link, or the link itself, yields the anchor; anything else, an
 * anchor without an address and a missing target yield null.
 */
import { describe, expect, it } from 'vitest'
import { anchorOf } from '@/lib/anchor'

/**
 * Builds a link holding a span, and a span outside any link.
 *
 * @param href - The link's address, omitted for an anchor without one.
 * @returns The link, the span inside it and the span outside.
 */
const build = (href?: string) => {
  const link = document.createElement('a')
  if (href !== undefined) {
    link.setAttribute('href', href)
  }
  const inside = document.createElement('span')
  link.append(inside)
  return { link, inside, outside: document.createElement('span') }
}

describe('anchorOf', () => {
  it('finds the link around the element, and the link itself', () => {
    const { link, inside } = build('/clients')

    expect(anchorOf(inside)).toBe(link)
    expect(anchorOf(link)).toBe(link)
  })

  it('is null outside a link, for an anchor without an address and for no target', () => {
    expect(anchorOf(build('/clients').outside)).toBeNull()
    expect(anchorOf(build().inside)).toBeNull()
    expect(anchorOf(null)).toBeNull()
  })
})
