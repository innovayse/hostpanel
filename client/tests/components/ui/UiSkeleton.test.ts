/**
 * Unit tests for UiSkeleton: a single line, several lines with a shorter last one, the block and
 * the circle, and the accessible loading status.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiSkeleton from '~/components/ui/UiSkeleton.vue'
import { UiSkeletonShape, UiSkeletonWidth } from '~/utils/ui/enums'

describe('UiSkeleton', () => {
  it('is one pulsing line by default and announces loading', () => {
    const wrapper = mountWithKit(UiSkeleton)

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['animate-pulse', 'h-4', 'w-full']))
    expect(wrapper.attributes('role')).toBe('status')
    expect(wrapper.attributes('aria-label')).toBe('ui.loading')
  })

  it('draws several lines with a shorter last one', () => {
    const wrapper = mountWithKit(UiSkeleton, { props: { lines: 3 } })

    const lines = wrapper.findAll('[data-testid="ui-skeleton-line"]')
    expect(lines).toHaveLength(3)
    expect(lines[0]?.classes()).toContain('w-full')
    expect(lines[2]?.classes()).toContain('w-2/3')
  })

  it('draws a block', () => {
    const wrapper = mountWithKit(UiSkeleton, { props: { shape: UiSkeletonShape.Block } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['rounded-xl', 'w-full']))
  })

  it('draws a circle', () => {
    const wrapper = mountWithKit(UiSkeleton, { props: { shape: UiSkeletonShape.Circle } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['rounded-full', 'h-10', 'w-10']))
  })

  it.each([
    [UiSkeletonWidth.ThreeQuarters, 'w-3/4'],
    [UiSkeletonWidth.Heading, 'w-64'],
  ])('takes the %s width on a block and a line', (width, expected) => {
    expect(mountWithKit(UiSkeleton, { props: { shape: UiSkeletonShape.Block, width } }).classes()).toContain(expected)
    expect(mountWithKit(UiSkeleton, { props: { width } }).classes()).toContain(expected)
  })

  it('ignores lines for a block', () => {
    const wrapper = mountWithKit(UiSkeleton, { props: { shape: UiSkeletonShape.Block, lines: 4 } })

    expect(wrapper.findAll('[data-testid="ui-skeleton-line"]')).toHaveLength(0)
  })
})
