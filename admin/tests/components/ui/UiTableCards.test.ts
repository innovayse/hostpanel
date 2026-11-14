/**
 * Unit tests for the card layout of UiTable (below `sm`): the header registration gives each cell its caption and role.
 */
import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { UiCardRole, cardCellClasses } from '@/utils/ui/table'
import { useRowSelection } from '@/composables/useRowSelection'
import { createTestI18n } from '../../helpers/i18n'

/** Global options giving the table its i18n plugin. */
const global = () => ({ plugins: [createTestI18n()] })

/**
 * A table with a title, a field, a badge, a hidden and an actions column and one row.
 *
 * @param tableProps Props of the table.
 * @param rowProps Props of the body row.
 * @returns A component to mount.
 */
const cardHarness = (tableProps: Record<string, unknown> = {}, rowProps: Record<string, unknown> = {}) => defineComponent({
  render: () => h(UiTable, tableProps, {
    default: () => [
      h(UiTableHead, null, { default: () => h(UiTableRow, null, { default: () => [
        h(UiTableHeader, { cardRole: UiCardRole.Title }, { default: () => 'Name' }),
        h(UiTableHeader, null, { default: () => 'Email' }),
        h(UiTableHeader, { cardRole: UiCardRole.Badge }, { default: () => 'Status' }),
        h(UiTableHeader, { cardRole: UiCardRole.Hidden }, { default: () => 'Id' }),
        h(UiTableHeader, { actions: true }, { default: () => 'Actions' }),
      ] }) }),
      h(UiTableBody, null, { default: () => h(UiTableRow, { ...rowProps, 'data-testid': 'row' }, { default: () => [
        h(UiTableCell, null, { default: () => 'Ada' }),
        h(UiTableCell, null, { default: () => 'ada@x.io' }),
        h(UiTableCell, null, { default: () => 'Active' }),
        h(UiTableCell, null, { default: () => '7' }),
        h(UiTableCell, { actions: true }, { default: () => 'Edit' }),
      ] }) }),
    ],
  }),
})

describe('UiTable cards', () => {
  it('gives every cell the role and caption of its header column', async () => {
    const wrapper = mount(cardHarness(), { global: global() })
    await flushPromises()
    const cells = wrapper.findAll('[data-testid="row"] td')

    expect(cells[0]!.classes()).toContain('max-sm:font-semibold')
    expect(cells[1]!.classes()).toContain('max-sm:before:content-[attr(data-label)]')
    expect(cells[1]!.attributes('data-label')).toBe('Email')
    expect(cells[2]!.classes()).toContain('max-sm:order-3')
    expect(cells[3]!.classes()).toContain('max-sm:hidden')
    expect(cells[4]!.classes()).toContain('max-sm:border-t')
  })

  it('draws the body row as a card and drops the header captions', async () => {
    const wrapper = mount(cardHarness(), { global: global() })
    await flushPromises()

    expect(wrapper.find('[data-testid="row"]').classes()).toContain('max-sm:rounded-xl')
    expect(wrapper.find('table').classes()).toContain('max-sm:block')
    expect(wrapper.findAll('th').every((th) => th.classes().includes('max-sm:hidden'))).toBe(true)
  })

  it('keeps the checkbox on the card and the select-all cell in the header', async () => {
    const selection = useRowSelection(() => [1])
    const wrapper = mount(cardHarness({ selection }, { rowId: 1 }), { global: global() })
    await flushPromises()
    const checkboxCell = wrapper.find('[data-testid="row"] td')

    expect(checkboxCell.classes()).toContain('max-sm:order-1')
    expect(wrapper.find('thead th').classes()).not.toContain('max-sm:hidden')
    expect(wrapper.findAll('[data-testid="row"] td')[1]!.attributes('data-label')).toBe('Name')
  })

  it('draws loading placeholders as cards with the same roles and captions', async () => {
    const wrapper = mount(cardHarness({ loading: true, skeletonRows: 2 }), { global: global() })
    await flushPromises()
    const rows = wrapper.findAll('[data-testid="ui-table-skeleton"] tr')

    expect(rows).toHaveLength(2)
    expect(rows[0]!.classes()).toContain('max-sm:rounded-xl')
    expect(rows[0]!.findAll('td')[1]!.attributes('data-label')).toBe('Email')
    expect(rows[0]!.findAll('td')[3]!.classes()).toContain('max-sm:hidden')
  })

  it('leaves a cell with no header column without card classes', () => {
    expect(cardCellClasses(undefined)).toBe('')
  })

  it('reads the caption of a sortable header without its sort arrow', async () => {
    const wrapper = mount(defineComponent({
      render: () => h(UiTable, null, { default: () => [
        h(UiTableHead, null, { default: () => h(UiTableRow, null, { default: () => h(UiTableHeader, { sortable: true }, { default: () => 'Date' }) }) }),
        h(UiTableBody, null, { default: () => h(UiTableRow, { 'data-testid': 'row' }, { default: () => h(UiTableCell, null, { default: () => 'x' }) }) }),
      ] }),
    }), { global: global() })
    await flushPromises()

    expect(wrapper.find('[data-testid="row"] td').attributes('data-label')).toBe('Date')
  })
})
