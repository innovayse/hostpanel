/**
 * Unit tests for UiHeading: semantic level and visual look are independent.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiHeading from '@/components/ui/UiHeading.vue'

describe('UiHeading', () => {
  it('renders an h2 by default', () => {
    const wrapper = mount(UiHeading, { slots: { default: 'Title' } })

    expect(wrapper.element.tagName).toBe('H2')
    expect(wrapper.text()).toBe('Title')
  })

  it.each([1, 3, 6] as const)('renders the matching element for level %i', (level) => {
    const wrapper = mount(UiHeading, { props: { level } })

    expect(wrapper.element.tagName).toBe(`H${level}`)
  })

  it('passes classes through', () => {
    const wrapper = mount(UiHeading, { attrs: { class: 'text-xl' } })

    expect(wrapper.classes()).toContain('text-xl')
  })

  it('takes its look from the variant, whatever the level', () => {
    const wrapper = mount(UiHeading, { props: { level: 3, variant: 'page' } })

    expect(wrapper.element.tagName).toBe('H3')
    expect(wrapper.classes()).toEqual(expect.arrayContaining(['font-display', 'font-semibold']))
  })
})
