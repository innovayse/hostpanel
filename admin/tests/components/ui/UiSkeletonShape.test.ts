/**
 * Unit tests for UiSkeletonShape: one placeholder shape for each part a report's data fills in (a map, lines of text,
 * a notice, small print).
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiSkeletonShape from '@/components/ui/UiSkeletonShape.vue'
import { SkeletonPartKind } from '@/utils/ui/skeletonSchema'

/**
 * Mounts one shape.
 *
 * @param kind - The part the shape stands in for.
 * @returns The mounted wrapper.
 */
const mountShape = (kind: typeof SkeletonPartKind.Map | typeof SkeletonPartKind.Lines | typeof SkeletonPartKind.Alert | typeof SkeletonPartKind.Note) =>
  mount(UiSkeletonShape, { props: { kind } })

describe('UiSkeletonShape', () => {
  it('draws a card with a map shaped bar for the map', () => {
    const wrapper = mountShape(SkeletonPartKind.Map)

    expect(wrapper.get('[data-testid="ui-skeleton-map"]').attributes('aria-hidden')).toBe('true')
    expect(wrapper.get('[data-testid="ui-skeleton-map"] [data-testid="ui-skeleton-bar"]').classes()).toContain('aspect-[1010/666]')
  })

  it('draws a line of text for the lines', () => {
    const wrapper = mountShape(SkeletonPartKind.Lines)

    expect(wrapper.findAll('[data-testid="ui-skeleton-lines"] [data-testid="ui-skeleton-bar"]')).toHaveLength(1)
  })

  it('draws a full width bar for the notice', () => {
    expect(mountShape(SkeletonPartKind.Alert).get('[data-testid="ui-skeleton-alert"]').classes()).toContain('w-full')
  })

  it('draws a short bar for the small print', () => {
    expect(mountShape(SkeletonPartKind.Note).get('[data-testid="ui-skeleton-note"]').classes()).toContain('h-4')
  })

  it('draws one shape and nothing else', () => {
    expect(mountShape(SkeletonPartKind.Note).findAll('[data-testid^="ui-skeleton-"]:not([data-testid="ui-skeleton-bar"])')).toHaveLength(1)
  })
})
