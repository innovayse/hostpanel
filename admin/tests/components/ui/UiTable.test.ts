/**
 * Unit tests for UiTable: native table, status row for loading and empty, shared density.
 */
import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import { mount } from '@vue/test-utils'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { createTestI18n } from '../../helpers/i18n'

/** Global options giving the table its i18n plugin. */
const global = () => ({ plugins: [createTestI18n()] })

describe('UiTable', () => {
  it('renders the native table with its slot', () => {
    const wrapper = mount(UiTable, { slots: { default: '<tbody><tr><td>content</td></tr></tbody>' }, global: global() })

    expect(wrapper.find('table').exists()).toBe(true)
    expect(wrapper.text()).toBe('content')
  })

  it('scrolls a table wider than its container inside its own wrapper', () => {
    const wrapper = mount(UiTable, { global: global() })

    expect(wrapper.element.tagName).toBe('DIV')
    expect(wrapper.classes()).toContain('overflow-x-auto')
    expect(wrapper.classes()).toContain('max-w-full')
    expect(wrapper.get('table').element.parentElement).toBe(wrapper.element)
  })

  it('passes classes and data attributes through', () => {
    const wrapper = mount(UiTable, { attrs: { class: 'x-class', 'data-testid': 'probe' }, global: global() })

    expect(wrapper.get('table').classes()).toContain('x-class')
    expect(wrapper.get('table').attributes('data-testid')).toBe('probe')
  })

  it('shows the empty message in a full-width row', () => {
    const wrapper = mount(UiTable, { props: { empty: true, colspan: 4 }, global: global() })

    expect(wrapper.text()).toBe('No data')
    expect(wrapper.get('td').attributes('colspan')).toBe('4')
  })

  it('lets a screen replace the empty message', () => {
    const wrapper = mount(UiTable, {
      props: { empty: true },
      slots: { empty: 'No clients yet' },
      global: global(),
    })

    expect(wrapper.text()).toBe('No clients yet')
  })

  it('shows skeleton lines instead of the message while loading', () => {
    const wrapper = mount(UiTable, { props: { loading: true, empty: true }, global: global() })

    expect(wrapper.findAll('.animate-pulse')).toHaveLength(3)
    expect(wrapper.text()).not.toContain('No data')
  })

  it('draws no status row when there is data', () => {
    expect(mount(UiTable, { global: global() }).find('tbody').exists()).toBe(false)
  })

  it('hands its density to the header and data cells', () => {
    const Harness = defineComponent({
      render: () => h(UiTable, { density: 'compact' }, {
        default: () => [
          h(UiTableHead, null, { default: () => h(UiTableRow, null, { default: () => h(UiTableHeader, null, { default: () => 'A' }) }) }),
          h(UiTableBody, null, { default: () => h(UiTableRow, null, { default: () => h(UiTableCell, null, { default: () => '1' }) }) }),
        ],
      }),
    })

    const wrapper = mount(Harness, { global: global() })

    expect(wrapper.get('th').classes()).toContain('px-3')
    expect(wrapper.get('td').classes()).toEqual(expect.arrayContaining(['px-3', 'py-2']))
  })

  it('pads its cells by default so a table never renders without spacing', () => {
    const Harness = defineComponent({
      render: () => h(UiTable, null, {
        default: () => [
          h(UiTableHead, null, { default: () => h(UiTableRow, null, { default: () => h(UiTableHeader, null, { default: () => 'A' }) }) }),
          h(UiTableBody, null, { default: () => h(UiTableRow, null, { default: () => h(UiTableCell, null, { default: () => '1' }) }) }),
        ],
      }),
    })

    const wrapper = mount(Harness, { global: global() })

    expect(wrapper.get('th').classes()).toEqual(expect.arrayContaining(['px-4', 'py-3']))
    expect(wrapper.get('td').classes()).toEqual(expect.arrayContaining(['px-4', 'py-3']))
  })
})
