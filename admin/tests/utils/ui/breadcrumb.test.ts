/**
 * Unit tests for the breadcrumb fold: a short trail is left alone, a long one keeps its first step and the steps
 * nearest the current page and hides the ones between.
 */
import { describe, expect, it } from 'vitest'
import { BREADCRUMB_MAX_VISIBLE, foldBreadcrumb } from '@/utils/ui/breadcrumb'
import type { UiBreadcrumbItem } from '@/utils/ui/breadcrumb'

/**
 * Builds a trail of the given labels.
 *
 * @param labels - One label per step.
 * @returns The steps, each with a link except the last.
 */
const trail = (...labels: string[]): UiBreadcrumbItem[] =>
  labels.map((label, index) => (index < labels.length - 1 ? { label, to: `/${label}` } : { label }))

/**
 * Reads a fold back as three label lists.
 *
 * @param items - The trail to fold.
 * @param maxVisible - The limit, the default when omitted.
 * @returns The labels of the head, the hidden steps and the tail.
 */
const folded = (items: UiBreadcrumbItem[], maxVisible?: number) => {
  const fold = foldBreadcrumb(items, maxVisible)
  return { head: fold.head.map((i) => i.label), hidden: fold.hidden.map((i) => i.label), tail: fold.tail.map((i) => i.label) }
}

describe('foldBreadcrumb', () => {
  it('keeps a trail of up to the limit whole, in the head', () => {
    expect(BREADCRUMB_MAX_VISIBLE).toBe(4)
    expect(folded(trail('a', 'b', 'c', 'd'))).toEqual({ head: ['a', 'b', 'c', 'd'], hidden: [], tail: [] })
    expect(folded(trail('a'))).toEqual({ head: ['a'], hidden: [], tail: [] })
  })

  it('keeps the first step and the last two of a longer trail, and hides the middle', () => {
    expect(folded(trail('a', 'b', 'c', 'd', 'e'))).toEqual({ head: ['a'], hidden: ['b', 'c'], tail: ['d', 'e'] })
    expect(folded(trail('a', 'b', 'c', 'd', 'e', 'f'))).toEqual({ head: ['a'], hidden: ['b', 'c', 'd'], tail: ['e', 'f'] })
  })

  it('follows the limit it is given, and never hides a single step', () => {
    expect(folded(trail('a', 'b', 'c', 'd'), 3)).toEqual({ head: ['a'], hidden: ['b', 'c'], tail: ['d'] })
    expect(folded(trail('a', 'b', 'c'), 3)).toEqual({ head: ['a', 'b', 'c'], hidden: [], tail: [] })
  })

  it('keeps the current page, the last step, in the tail of a folded trail', () => {
    const fold = foldBreadcrumb(trail('a', 'b', 'c', 'd', 'e'))

    expect(fold.tail[fold.tail.length - 1]).toEqual({ label: 'e' })
  })
})
