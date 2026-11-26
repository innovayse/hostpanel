// @vitest-environment happy-dom
/**
 * Component tests for the callout box: each type draws an icon of the icon library (an svg), never a text character,
 * and carries its type class and its slot.
 */
import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import Callout from '../.vitepress/theme/components/Callout.vue'

describe('Callout', () => {
  it.each(['note', 'tip', 'warning', 'danger'] as const)('draws an svg icon and no text glyph for the %s type', (type) => {
    const wrapper = mount(Callout, { props: { type }, slots: { default: 'Body text' } })

    expect(wrapper.classes()).toContain(`callout--${type}`)
    expect(wrapper.find('svg.callout__icon').exists()).toBe(true)
    expect(wrapper.find('svg.callout__icon').attributes('aria-hidden')).toBe('true')
    expect(wrapper.text()).toBe('Body text')
  })

  it('is a note by default', () => {
    expect(mount(Callout).classes()).toContain('callout--note')
  })

  it('draws a different icon for each type', () => {
    /** The drawn svg of each callout type. */
    const shapes = (['note', 'tip', 'warning', 'danger'] as const).map((type) => mount(Callout, { props: { type } }).find('svg').html())

    expect(new Set(shapes).size).toBe(4)
  })
})
