/**
 * Unit tests for UiTableHead: native `<thead>` root, slot content and attribute pass-through.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiTableHead from '@/components/ui/UiTableHead.vue'

describe('UiTableHead', () => {
  it('renders the native thead with its slot', () => {
    const wrapper = mount(UiTableHead, { slots: { default: 'content' } })

    expect(wrapper.element.tagName).toBe('THEAD')
    expect(wrapper.text()).toBe('content')
  })

  it('sticks to the top of the table box above the rows', () => {
    expect(mount(UiTableHead).classes()).toEqual(expect.arrayContaining(['sticky', 'top-0', 'z-10']))
  })

  it('passes classes and data attributes through', () => {
    const wrapper = mount(UiTableHead, { attrs: { class: 'x-class', 'data-testid': 'probe' } })

    expect(wrapper.classes()).toContain('x-class')
    expect(wrapper.attributes('data-testid')).toBe('probe')
  })
})
