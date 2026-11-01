/**
 * Unit tests for UiTableCell: native td, alignment, muted tone and density.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiTableCell from '@/components/ui/UiTableCell.vue'

describe('UiTableCell', () => {
  it('renders the native td with its slot', () => {
    const wrapper = mount(UiTableCell, { slots: { default: 'content' } })

    expect(wrapper.element.tagName).toBe('TD')
    expect(wrapper.text()).toBe('content')
  })

  it('passes classes and data attributes through', () => {
    const wrapper = mount(UiTableCell, { attrs: { class: 'x-class', 'data-testid': 'probe' } })

    expect(wrapper.classes()).toContain('x-class')
    expect(wrapper.attributes('data-testid')).toBe('probe')
  })

  it('aligns and greys on request', () => {
    const wrapper = mount(UiTableCell, { props: { align: 'right', muted: true } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['text-end', 'text-text-muted']))
  })

  it('adds no padding outside a table', () => {
    expect(mount(UiTableCell).classes()).toEqual(['[:where(&)]:min-w-24'])
  })

  it('keeps a readable minimum width that a caller can override', () => {
    const wrapper = mount(UiTableCell)

    expect(wrapper.classes()).toContain('[:where(&)]:min-w-24')
  })
})
