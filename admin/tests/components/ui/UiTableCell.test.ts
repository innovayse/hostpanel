/**
 * Unit tests for UiTableCell: native td, alignment, muted tone and density.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiTableCell from '@/components/ui/UiTableCell.vue'

describe('UiTableCell', () => {
  it('hides the column below its breakpoint and shows it as a table cell from there', () => {
    const wrapper = mount(UiTableCell, { props: { hideBelow: 'sm' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['hidden', 'sm:table-cell']))
    expect(mount(UiTableCell).classes()).not.toContain('hidden')
    expect(mount(UiTableCell, { props: { hideBelow: 'xl' } }).classes()).toContain('xl:table-cell')
  })

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

  it('stresses the value with semibold primary text when strong', () => {
    expect(mount(UiTableCell, { props: { strong: true } }).classes()).toEqual(expect.arrayContaining(['font-semibold', 'text-text-primary']))
  })

  it('right aligns a numeric cell with tabular figures', () => {
    expect(mount(UiTableCell, { props: { numeric: true } }).classes()).toEqual(expect.arrayContaining(['text-end', 'tabular-nums']))
  })

  it('gives the actions cell the shared fixed width, right aligned, over any align', () => {
    const classes = mount(UiTableCell, { props: { actions: true, align: 'left' } }).classes()

    expect(classes).toEqual(expect.arrayContaining(['w-16', 'sm:w-28', 'text-end', 'whitespace-nowrap']))
    expect(classes).not.toContain('text-start')
  })

  it('adds no padding outside a table and aligns left like the header, with no card layout', () => {
    const wrapper = mount(UiTableCell)

    expect(wrapper.classes()).toEqual(['text-start'])
    expect(wrapper.find('span').exists()).toBe(false)
  })

  it('is a cell for assistive technology whatever the layout', () => {
    expect(mount(UiTableCell).attributes('role')).toBe('cell')
  })

  it('takes the remaining width and lets its content truncate when asked to', () => {
    expect(mount(UiTableCell, { props: { truncate: true } }).classes()).toEqual(expect.arrayContaining(['w-full', 'max-w-0']))
  })
})
