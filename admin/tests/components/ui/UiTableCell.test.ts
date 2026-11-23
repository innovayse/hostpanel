/**
 * Unit tests for UiTableCell: native td, alignment, muted tone and density.
 */
import { describe, expect, it } from 'vitest'
import { defineComponent, h, nextTick } from 'vue'
import { mount } from '@vue/test-utils'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { UiColumnKind, UiColumnSize } from '@/utils/ui/table'
import { createTestI18n } from '../../helpers/i18n'

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

  it('gives the actions cell its right aligned one line shape over any align (the width is the compact size of its column)', () => {
    const classes = mount(UiTableCell, { props: { actions: true, align: 'left' } }).classes()

    expect(classes).toEqual(expect.arrayContaining(['text-end', 'whitespace-nowrap']))
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

  it('follows the size of its column, read from the header at its position', async () => {
    const Harness = defineComponent({
      render: () => h(UiTable, null, {
        default: () => [
          h(UiTableHead, null, { default: () => h(UiTableRow, null, { default: () => [
            h(UiTableHeader, { size: UiColumnSize.Compact }, { default: () => 'Id' }),
            h(UiTableHeader, { size: UiColumnSize.Grow }, { default: () => 'Name' }),
            h(UiTableHeader, null, { default: () => 'Email' }),
          ] }) }),
          h(UiTableBody, null, { default: () => h(UiTableRow, null, { default: () => [
            h(UiTableCell, null, { default: () => '7' }),
            h(UiTableCell, null, { default: () => 'Ada' }),
            h(UiTableCell, null, { default: () => 'ada@x.io' }),
          ] }) }),
        ],
      }),
    })

    const wrapper = mount(Harness, { global: { plugins: [createTestI18n()] } })
    await nextTick()
    const cells = wrapper.findAll('tbody td')

    expect(cells[0]?.classes()).toEqual(expect.arrayContaining(['w-px', 'whitespace-nowrap']))
    expect(cells[1]?.classes()).toEqual(expect.arrayContaining(['w-[28%]', 'max-w-0', 'truncate']))
    expect(cells[2]?.classes()).toEqual(expect.arrayContaining(['w-[28%]', 'truncate']))
    expect(cells[2]?.classes()).not.toContain('w-px')
  })

  it('shows the full text of a cut cell as a tooltip, and nothing for a cell that fits or does not truncate', async () => {
    const Harness = defineComponent({
      render: () => h(UiTable, null, {
        default: () => [
          h(UiTableHead, null, { default: () => h(UiTableRow, null, { default: () => [
            h(UiTableHeader, null, { default: () => 'Name' }),
            h(UiTableHeader, { kind: UiColumnKind.Date }, { default: () => 'Date' }),
          ] }) }),
          h(UiTableBody, null, { default: () => h(UiTableRow, null, { default: () => [
            h(UiTableCell, null, { default: () => 'A very long name' }),
            h(UiTableCell, null, { default: () => '2026-01-01' }),
          ] }) }),
        ],
      }),
    })
    const wrapper = mount(Harness, { global: { plugins: [createTestI18n()] } })
    await nextTick()
    const [name, date] = wrapper.findAll('tbody td')
    /** Gives an element the scroll and client widths a layout engine would measure. */
    const measure = (element: Element | undefined, scrollWidth: number, clientWidth: number): void => {
      Object.defineProperty(element, 'scrollWidth', { configurable: true, value: scrollWidth })
      Object.defineProperty(element, 'clientWidth', { configurable: true, value: clientWidth })
    }

    measure(name?.element, 200, 80)
    await name?.trigger('mouseenter')
    expect(name?.attributes('title')).toBe('A very long name')

    measure(name?.element, 80, 80)
    await name?.trigger('mouseenter')
    expect(name?.attributes('title')).toBe('')

    measure(date?.element, 200, 80)
    await date?.trigger('mouseenter')
    expect(date?.attributes('title')).toBeUndefined()
  })

  it('has no size of its own outside a table', () => {
    expect(mount(UiTableCell).classes()).not.toContain('whitespace-nowrap')
  })
})
