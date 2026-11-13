/**
 * Unit tests for UiSkeletonBar: the resting fill, the shimmer sweep and its reduced-motion stop.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'

describe('UiSkeletonBar', () => {
  it('draws a clipped bar on the hover surface token and keeps the caller classes', () => {
    const wrapper = mount(UiSkeletonBar, { attrs: { class: 'h-3 w-full rounded' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['overflow-hidden', 'bg-surface-hover', 'h-3', 'w-full', 'rounded']))
  })

  it('sweeps a gradient band across the bar', () => {
    const band = mount(UiSkeletonBar).get('[data-testid="ui-skeleton-bar"] > div')

    expect(band.classes()).toContain('animate-[shimmer_1.6s_ease-in-out_infinite]')
    expect(band.classes()).toContain('bg-linear-to-r')
  })

  it('stops the sweep for visitors who ask for reduced motion', () => {
    const band = mount(UiSkeletonBar).get('[data-testid="ui-skeleton-bar"] > div')

    expect(band.classes()).toContain('motion-reduce:animate-none')
  })
})
