/**
 * Unit tests for the main landmark of the home page: the home content is marked only on the home
 * page, only when it is on screen, and the mark names the right element and role.
 */
import { describe, expect, it } from 'vitest'
import { HOME_SELECTOR, markHomeAsMain } from '../.vitepress/theme/landmark'
import type { LandmarkRoot, MarkableElement } from '../.vitepress/theme/landmark'

/** A fake element that records the attributes set on it. */
const fakeElement = (): MarkableElement & { attributes: Record<string, string> } => {
  const attributes: Record<string, string> = {}
  return {
    attributes,
    setAttribute: (name, value) => {
      attributes[name] = value
    },
  }
}

/** A fake document that holds one element for one selector and records every selector asked for. */
const fakeRoot = (element: MarkableElement | null): LandmarkRoot & { asked: string[] } => {
  const asked: string[] = []
  return {
    asked,
    querySelector: (selector) => {
      asked.push(selector)
      return selector === HOME_SELECTOR ? element : null
    },
  }
}

describe('markHomeAsMain', () => {
  it('marks the home content as the main landmark', () => {
    const home = fakeElement()

    expect(markHomeAsMain(fakeRoot(home), true)).toBe(true)
    expect(home.attributes).toEqual({ role: 'main' })
  })

  it('looks for the element of the home layout', () => {
    const root = fakeRoot(fakeElement())

    markHomeAsMain(root, true)

    expect(root.asked).toEqual(['.VPHome'])
  })

  it('does nothing on a page that is not the home page', () => {
    const home = fakeElement()
    const root = fakeRoot(home)

    expect(markHomeAsMain(root, false)).toBe(false)
    expect(root.asked).toEqual([])
    expect(home.attributes).toEqual({})
  })

  it('does nothing while the home content is not on screen', () => {
    expect(markHomeAsMain(fakeRoot(null), true)).toBe(false)
  })

  it('can run again on the same element without changing the result', () => {
    const home = fakeElement()
    const root = fakeRoot(home)

    markHomeAsMain(root, true)
    markHomeAsMain(root, true)

    expect(home.attributes).toEqual({ role: 'main' })
  })
})
