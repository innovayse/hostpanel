/**
 * Unit tests for the scroll to a `#hash` heading: reading the id from the hash, scrolling to the
 * element, and the settler that scrolls again until it is cancelled.
 */
import { describe, expect, it } from 'vitest'
import { createHashSettler, scrollToHash, targetIdOf } from '../.vitepress/theme/hashScroll'
import type { HashScrollRoot, ScrollTarget } from '../.vitepress/theme/hashScroll'

/** A fake element that records the scrolls made to it. */
const fakeTarget = (): ScrollTarget & { scrolls: Array<{ block: string; behavior: string }> } => {
  const scrolls: Array<{ block: string; behavior: string }> = []
  return {
    scrolls,
    scrollIntoView: (options) => {
      scrolls.push(options)
    },
  }
}

/** A fake document that holds one element for one id. */
const fakeRoot = (id: string, target: ScrollTarget | null): HashScrollRoot => ({
  getElementById: (asked) => (asked === id ? target : null),
})

describe('targetIdOf', () => {
  it('reads the id after the hash sign', () => {
    expect(targetIdOf('#which-releases-have-versioned-docs')).toBe('which-releases-have-versioned-docs')
  })

  it('decodes a percent encoded id', () => {
    expect(targetIdOf('#%D5%A2%D5%A1%D6%80')).toBe('բար')
  })

  it('returns null for an empty hash, a lone hash sign and an id that cannot be decoded', () => {
    expect(targetIdOf('')).toBeNull()
    expect(targetIdOf('#')).toBeNull()
    expect(targetIdOf('#%E0%A4%A')).toBeNull()
  })
})

describe('scrollToHash', () => {
  it('scrolls the heading to the top, instantly', () => {
    const target = fakeTarget()

    expect(scrollToHash(fakeRoot('build-settings', target), '#build-settings')).toBe(true)
    expect(target.scrolls).toEqual([{ block: 'start', behavior: 'instant' }])
  })

  it('does nothing when the element is missing or the hash is empty', () => {
    const target = fakeTarget()

    expect(scrollToHash(fakeRoot('build-settings', null), '#build-settings')).toBe(false)
    expect(scrollToHash(fakeRoot('build-settings', target), '')).toBe(false)
    expect(target.scrolls).toHaveLength(0)
  })
})

describe('createHashSettler', () => {
  it('scrolls each time it is run', () => {
    let calls = 0
    const settler = createHashSettler(() => {
      calls += 1
      return true
    })

    expect(settler.run()).toBe(true)
    expect(settler.run()).toBe(true)
    expect(calls).toBe(2)
  })

  it('stops scrolling once it is cancelled, so a reader who scrolled is left alone', () => {
    let calls = 0
    const settler = createHashSettler(() => {
      calls += 1
      return true
    })

    settler.run()
    settler.cancel()

    expect(settler.run()).toBe(false)
    expect(calls).toBe(1)
  })
})
