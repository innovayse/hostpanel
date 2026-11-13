/**
 * Unit tests for UiTableBody: native tbody and the optional row divider.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiTableBody from '@/components/ui/UiTableBody.vue'

describe('UiTableBody', () => {
  it('renders the native tbody with its slot', () => {
    const wrapper = mount(UiTableBody, { slots: { default: '<tr><td>content</td></tr>' } })

    expect(wrapper.element.tagName).toBe('TBODY')
    expect(wrapper.text()).toBe('content')
  })

  it('passes classes and data attributes through', () => {
    const wrapper = mount(UiTableBody, { attrs: { class: 'x-class', 'data-testid': 'probe' } })

    expect(wrapper.classes()).toContain('x-class')
    expect(wrapper.attributes('data-testid')).toBe('probe')
  })

  it('draws dividers between rows only when asked', () => {
    expect(mount(UiTableBody).classes()).not.toContain('divide-y')
    expect(mount(UiTableBody, { props: { divided: true } }).classes()).toEqual(expect.arrayContaining(['divide-y', 'divide-border-subtle']))
  })
})
