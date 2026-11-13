/**
 * Unit tests for UiLabel: native label, named looks, attribute pass-through.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiLabel from '@/components/ui/UiLabel.vue'

describe('UiLabel', () => {
  it('renders the native label with its slot', () => {
    const wrapper = mount(UiLabel, { slots: { default: 'content' } })

    expect(wrapper.element.tagName).toBe('LABEL')
    expect(wrapper.text()).toBe('content')
  })

  it('passes classes and data attributes through', () => {
    const wrapper = mount(UiLabel, { attrs: { class: 'x-class', 'data-testid': 'probe' } })

    expect(wrapper.classes()).toContain('x-class')
    expect(wrapper.attributes('data-testid')).toBe('probe')
  })

  it('draws the field caption look', () => {
    const wrapper = mount(UiLabel, { props: { variant: 'field' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['text-[0.8125rem]', 'font-medium', 'text-text-muted']))
  })

  it('draws the inline look for a checkbox row', () => {
    const wrapper = mount(UiLabel, { props: { variant: 'inline' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['flex', 'cursor-pointer']))
  })

  it('draws a span for a caption tied to no control', () => {
    const wrapper = mount(UiLabel, { props: { as: 'span', variant: 'field' }, slots: { default: 'caption' } })

    expect(wrapper.element.tagName).toBe('SPAN')
    expect(wrapper.text()).toBe('caption')
    expect(wrapper.classes()).toContain('text-text-muted')
  })
})
