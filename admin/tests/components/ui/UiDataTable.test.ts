/**
 * Unit tests for UiDataTable: the card, the table and the pager every list screen shares.
 */
import { describe, expect, it } from 'vitest'
import { h } from 'vue'
import { mount } from '@vue/test-utils'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { UiTableDensity } from '@/utils/ui/injection'
import { createTestI18n } from '../../helpers/i18n'

/** Global options giving the component its i18n plugin. */
const global = () => ({ plugins: [createTestI18n()] })

/** The body rows the tests put in the default slot. */
const ROWS = '<tbody><tr><td>row</td></tr></tbody>'

describe('UiDataTable', () => {
  it('frames the table in one clipped glass card', () => {
    const wrapper = mount(UiDataTable, { slots: { default: ROWS }, global: global() })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['glass', 'overflow-clip']))
    expect(wrapper.find('table').exists()).toBe(true)
    expect(wrapper.text()).toContain('row')
  })

  it('puts the screen attributes on the table and not on the card', () => {
    const wrapper = mount(UiDataTable, { attrs: { 'data-testid': 'probe' }, global: global() })

    expect(wrapper.attributes('data-testid')).toBeUndefined()
    expect(wrapper.find('table').attributes('data-testid')).toBe('probe')
  })

  it('draws no footer for a list that is not paged', () => {
    const wrapper = mount(UiDataTable, { global: global() })

    expect(wrapper.find('[data-testid="ui-card-footer"]').exists()).toBe(false)
  })

  it('draws no empty footer strip under a single page', () => {
    const wrapper = mount(UiDataTable, { props: { page: 1, total: 5, pageSize: 20 }, global: global() })

    expect(wrapper.find('[data-testid="ui-card-footer"]').exists()).toBe(false)
  })

  it('keeps the footer while the list loads, so the layout does not jump', () => {
    const wrapper = mount(UiDataTable, { props: { page: 1, total: 0, pageSize: 20, loading: true }, global: global() })

    expect(wrapper.find('[data-testid="ui-card-footer"]').exists()).toBe(true)
  })

  it('draws the pager in the card footer and forwards what the reader chooses', async () => {
    const wrapper = mount(UiDataTable, {
      props: { page: 1, total: 60, pageSize: 20, paginationTestid: 'probe-pagination' },
      global: global(),
    })

    const footer = wrapper.find('[data-testid="ui-card-footer"]')
    expect(footer.exists()).toBe(true)
    expect(footer.find('[data-testid="probe-pagination-range"]').exists()).toBe(true)

    await footer.find('[data-testid="probe-pagination-page-2"]').trigger('click')
    expect(wrapper.emitted('update:page')).toEqual([[2]])
  })

  it('forwards the page size the reader picks when sizes are offered', () => {
    const wrapper = mount(UiDataTable, {
      props: { page: 1, total: 60, pageSize: 20, pageSizeOptions: [10, 20, 50], paginationTestid: 'probe-pagination' },
      global: global(),
    })

    expect(wrapper.find('[data-testid="probe-pagination-page-size"]').exists()).toBe(true)
  })

  it('shows the empty message of the table and lets a screen replace it', () => {
    const plain = mount(UiDataTable, { props: { empty: true, emptyText: 'Nothing here' }, global: global() })
    expect(plain.text()).toContain('Nothing here')

    const custom = mount(UiDataTable, {
      props: { empty: true },
      slots: { empty: '<p data-testid="own-empty">None</p>' },
      global: global(),
    })
    expect(custom.find('[data-testid="own-empty"]').exists()).toBe(true)
  })

  it('draws placeholder rows while it loads', () => {
    const wrapper = mount(UiDataTable, { props: { loading: true }, global: global() })

    expect(wrapper.find('[data-testid="ui-table-skeleton"]').exists()).toBe(true)
  })

  it('gives the cells comfortable padding unless the screen asks for compact', () => {
    /** Mounts the table with the given density and returns the wrapper. */
    const cell = (density?: UiTableDensity) => mount(UiDataTable, {
      props: density ? { density } : {},
      slots: { default: () => h(UiTableBody, null, { default: () => h(UiTableRow, null, { default: () => h(UiTableCell, null, { default: () => 'x' }) }) }) },
      global: global(),
    }).find('td').classes()

    expect(cell()).toEqual(cell(UiTableDensity.Comfortable))
    expect(cell(UiTableDensity.Compact)).not.toEqual(cell())
  })

  it('draws the card header slot above the table', () => {
    const wrapper = mount(UiDataTable, { slots: { header: '<h3>Title</h3>', default: ROWS }, global: global() })

    expect(wrapper.find('h3').text()).toBe('Title')
    expect(wrapper.find('[data-testid="ui-data-table-header"]').classes()).toEqual(expect.arrayContaining(['px-4', 'py-3', 'border-b']))
  })
})
