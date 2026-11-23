/**
 * Unit tests for UiLegendSwatch: a decorative block filled by a Tailwind class, as a square or a step of a scale.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiLegendSwatch from '@/components/ui/UiLegendSwatch.vue'
import { UiLegendSwatchShape } from '@/utils/ui/svgMap'

describe('UiLegendSwatch', () => {
  it('is a small square with the given fill by default', () => {
    const wrapper = mount(UiLegendSwatch, { props: { fill: 'bg-border' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['bg-border', 'size-3', 'rounded-sm']))
  })

  it('is a wide step of a scale on request', () => {
    const wrapper = mount(UiLegendSwatch, { props: { fill: 'bg-primary-300', shape: UiLegendSwatchShape.Step } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['bg-primary-300', 'w-5', 'first:rounded-l-sm']))
  })

  it('is hidden from assistive technology', () => {
    expect(mount(UiLegendSwatch, { props: { fill: 'bg-border' } }).attributes('aria-hidden')).toBe('true')
  })
})
