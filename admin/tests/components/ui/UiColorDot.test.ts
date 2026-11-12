/**
 * Unit tests for UiColorDot: a round SVG dot filled by attribute (no inline style), hidden from
 * assistive technology.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiColorDot from '@/components/ui/UiColorDot.vue'

describe('UiColorDot', () => {
  it('fills the circle with the colour and sets no inline style', () => {
    const wrapper = mount(UiColorDot, { props: { color: '#3B82F6' } })

    expect(wrapper.get('circle').attributes('fill')).toBe('#3B82F6')
    expect(wrapper.attributes('style')).toBeUndefined()
  })

  it('is decorative', () => {
    const wrapper = mount(UiColorDot, { props: { color: '#3B82F6' } })

    expect(wrapper.attributes('aria-hidden')).toBe('true')
  })
})
