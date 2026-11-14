/**
 * Unit tests for UiTableRow: native tr and its optional states.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiTableRow from '@/components/ui/UiTableRow.vue'

describe('UiTableRow', () => {
  it('renders the native tr with its slot', () => {
    const wrapper = mount(UiTableRow, { slots: { default: '<td>content</td>' } })

    expect(wrapper.element.tagName).toBe('TR')
    expect(wrapper.text()).toBe('content')
  })

  it('passes classes and data attributes through', () => {
    const wrapper = mount(UiTableRow, { attrs: { class: 'x-class', 'data-testid': 'probe' } })

    expect(wrapper.classes()).toContain('x-class')
    expect(wrapper.attributes('data-testid')).toBe('probe')
  })

  it('adds no state classes by default, only its card layout below sm', () => {
    const classes = mount(UiTableRow).classes()

    expect(classes.every((name) => name.startsWith('max-sm:'))).toBe(true)
    expect(classes).toContain('max-sm:rounded-xl')
  })

  it('tints on hover, marks the selection and draws the border on request', () => {
    const wrapper = mount(UiTableRow, { props: { hoverable: true, selected: true, bordered: true } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['hover:bg-surface-hover', 'bg-primary-500/10', 'border-b']))
  })
})
