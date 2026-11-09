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

  it('right aligns a numeric cell with tabular figures', () => {
    expect(mount(UiTableCell, { props: { numeric: true } }).classes()).toEqual(expect.arrayContaining(['text-end', 'tabular-nums']))
  })

  it('gives the actions cell the shared fixed width, right aligned, over any align', () => {
    const classes = mount(UiTableCell, { props: { actions: true, align: 'left' } }).classes()

    expect(classes).toEqual(expect.arrayContaining(['w-28', 'text-end', 'whitespace-nowrap']))
    expect(classes).not.toContain('text-start')
  })

  it('adds no padding outside a table and aligns left like the header', () => {
    expect(mount(UiTableCell).classes()).toEqual(['text-start'])
  })
})
