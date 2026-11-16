/**
 * Unit tests for the card layout of UiTable (below `sm`): each cell finds its header column by its position in the row, takes the
 * column's role and caption, the table keeps its ARIA roles, sorting stays reachable and editable tables work as cards.
 */
import { describe, expect, it, vi } from 'vitest'
import { defineComponent, h, nextTick, ref } from 'vue'
import type { VNodeChild } from 'vue'
import { createI18n, useI18n } from 'vue-i18n'
import { flushPromises, mount } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableFoot from '@/components/ui/UiTableFoot.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { useRowSelection } from '@/composables/useRowSelection'
import { messages } from '@/locales/messages'
import { UiCardRole, UiSortDirection } from '@/utils/ui/table'
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

/**
 * A table from header definitions and one body row of the given cells.
 *
 * @param headers Props and caption of each header.
 * @param cells Content and props of each cell of the row.
 * @param tableProps Props of the table.
 * @returns A component to mount.
 */
const tableOf = (
  headers: Array<{ caption: string; props?: Record<string, unknown> }>,
  cells: () => VNodeChild[],
  tableProps: Record<string, unknown> = {},
) => defineComponent({
  render: () => h(UiTable, tableProps, {
    default: () => [
      h(UiTableHead, null, { default: () => h(UiTableRow, null, { default: () => headers.map(({ caption, props }) => h(UiTableHeader, props, { default: () => caption })) }) }),
      h(UiTableBody, null, { default: () => h(UiTableRow, { 'data-testid': 'row' }, { default: cells }) }),
    ],
  }),
})

describe('UiTable cards', () => {
  it('gives every cell the role of its header column', async () => {
    const wrapper = mount(cardHarness(), { global: global() })
    await flushPromises()
    const cells = wrapper.findAll('[data-testid="row"] td')

    expect(cells[0]!.classes()).toContain('max-sm:font-semibold')
    expect(cells[1]!.classes()).toContain('max-sm:justify-between')
    expect(cells[2]!.classes()).toContain('max-sm:order-3')
    expect(cells[3]!.classes()).toContain('max-sm:hidden')
    expect(cells[4]!.classes()).toContain('max-sm:border-t')
  })

  it('names each value by the caption of its column in a real element, hidden from sm up', async () => {
    const wrapper = mount(cardHarness(), { global: global() })
    await flushPromises()
    const cells = wrapper.findAll('[data-testid="row"] td')

    expect(cells[0]!.find('span').text()).toBe('Name')
    expect(cells[0]!.find('span').classes()).toEqual(expect.arrayContaining(['sr-only', 'sm:hidden']))
    expect(cells[1]!.find('span').text()).toBe('Email')
    expect(cells[1]!.find('span').classes()).toEqual(expect.arrayContaining(['text-xs', 'sm:hidden']))
    expect(cells[2]!.find('span').text()).toBe('Status')
    expect(cells[3]!.find('span').exists()).toBe(false)
    expect(cells[4]!.find('span').exists()).toBe(false)
  })

  it('draws the body row as a card and drops the header captions', async () => {
    const wrapper = mount(cardHarness(), { global: global() })
    await flushPromises()

    expect(wrapper.find('[data-testid="row"]').classes()).toContain('max-sm:rounded-xl')
    expect(wrapper.find('table').classes()).toContain('max-sm:block')
    expect(wrapper.findAll('th').every((th) => th.classes().includes('max-sm:hidden'))).toBe(true)
  })

  it('keeps the table semantics of every part as explicit roles', async () => {
    const wrapper = mount(cardHarness({}, { selected: true }), { global: global() })
    await flushPromises()

    expect(wrapper.find('table').attributes('role')).toBe('table')
    expect(wrapper.find('thead').attributes('role')).toBe('rowgroup')
    expect(wrapper.find('tbody').attributes('role')).toBe('rowgroup')
    expect(wrapper.find('thead tr').attributes('role')).toBe('row')
    expect(wrapper.find('[data-testid="row"]').attributes('role')).toBe('row')
    expect(wrapper.find('th').attributes('role')).toBe('columnheader')
    expect(wrapper.find('[data-testid="row"] td').attributes('role')).toBe('cell')
  })

  it('gives the foot, the empty row and the loading rows their roles too', async () => {
    const foot = mount(defineComponent({
      render: () => h(UiTable, null, { default: () => h(UiTableFoot, null, { default: () => h(UiTableRow) }) }),
    }), { global: global() })
    const empty = mount(cardHarness({ empty: true }), { global: global() })
    const loading = mount(cardHarness({ loading: true, skeletonRows: 1 }), { global: global() })
    await flushPromises()

    expect(foot.find('tfoot').attributes('role')).toBe('rowgroup')
    expect(empty.findAll('tbody')[1]!.attributes('role')).toBe('rowgroup')
    expect(empty.findAll('tbody')[1]!.find('tr').attributes('role')).toBe('row')
    expect(empty.findAll('tbody')[1]!.find('td').attributes('role')).toBe('cell')
    expect(loading.find('[data-testid="ui-table-skeleton"]').attributes('role')).toBe('rowgroup')
    expect(loading.find('[data-testid="ui-table-skeleton"] tr').attributes('role')).toBe('row')
    expect(loading.find('[data-testid="ui-table-skeleton"] td').attributes('role')).toBe('cell')
  })

  it('keeps aria-selected on the row of a ticked card', async () => {
    const selection = useRowSelection(() => [1])
    selection.toggle(1, false)
    const wrapper = mount(cardHarness({ selection }, { rowId: 1 }), { global: global() })
    await flushPromises()

    expect(wrapper.find('[data-testid="row"]').attributes('role')).toBe('row')
    expect(wrapper.find('[data-testid="row"]').attributes('aria-selected')).toBe('true')
  })

  it('keeps the checkbox on the card and the select-all cell in the header', async () => {
    const selection = useRowSelection(() => [1])
    const wrapper = mount(cardHarness({ selection }, { rowId: 1 }), { global: global() })
    await flushPromises()
    const checkboxCell = wrapper.find('[data-testid="row"] td')

    expect(checkboxCell.classes()).toContain('max-sm:order-1')
    expect(checkboxCell.find('span').exists()).toBe(false)
    expect(wrapper.find('thead th').classes()).not.toContain('max-sm:hidden')
    expect(wrapper.findAll('[data-testid="row"] td')[1]!.find('span').text()).toBe('Name')
  })

  it('draws loading placeholders as cards with the same roles and captions', async () => {
    const wrapper = mount(cardHarness({ loading: true, skeletonRows: 2 }), { global: global() })
    await flushPromises()
    const rows = wrapper.findAll('[data-testid="ui-table-skeleton"] tr')

    expect(rows).toHaveLength(2)
    expect(rows[0]!.classes()).toContain('max-sm:rounded-xl')
    expect(rows[0]!.findAll('td')[1]!.find('span').text()).toBe('Email')
    expect(rows[0]!.findAll('td')[3]!.classes()).toContain('max-sm:hidden')
  })

  it('reads the caption of a sortable header without its sort arrow', async () => {
    const wrapper = mount(tableOf([{ caption: 'Date', props: { sortable: true } }], () => [h(UiTableCell, null, { default: () => 'x' })]), { global: global() })
    await flushPromises()

    expect(wrapper.find('[data-testid="row"] td span').text()).toBe('Date')
  })

  it('leaves a column hidden below a breakpoint out of the card', async () => {
    const wrapper = mount(tableOf(
      [{ caption: 'Name' }, { caption: 'Email', props: { hideBelow: 'lg' } }],
      () => [h(UiTableCell, null, { default: () => 'Ada' }), h(UiTableCell, { hideBelow: 'lg' }, { default: () => 'ada@x.io' })],
    ), { global: global() })
    await flushPromises()
    const cells = wrapper.findAll('[data-testid="row"] td')

    expect(cells[1]!.classes()).toEqual(expect.arrayContaining(['hidden', 'lg:table-cell', 'max-sm:hidden']))
    expect(cells[1]!.find('span').exists()).toBe(false)
  })

  it('shows a hidden below column on the card when its header sets a role itself', async () => {
    const wrapper = mount(tableOf(
      [{ caption: 'Name' }, { caption: 'Total', props: { hideBelow: 'sm', cardRole: UiCardRole.Field } }],
      () => [h(UiTableCell, null, { default: () => 'Ada' }), h(UiTableCell, { hideBelow: 'sm' }, { default: () => '$5' })],
    ), { global: global() })
    await flushPromises()
    const total = wrapper.findAll('[data-testid="row"] td')[1]!

    expect(total.classes()).toContain('max-sm:flex')
    expect(total.classes()).not.toContain('max-sm:hidden')
    expect(total.find('span').text()).toBe('Total')
  })

  it('takes the card role of an editable column: the caption over a full width control', async () => {
    const wrapper = mount(tableOf(
      [{ caption: 'Description', props: { cardRole: UiCardRole.Control } }],
      () => [h(UiTableCell, null, { default: () => h('input', { placeholder: 'x' }) })],
    ), { global: global() })
    await flushPromises()
    const cell = wrapper.find('[data-testid="row"] td')

    expect(cell.classes()).toEqual(expect.arrayContaining(['max-sm:flex', 'max-sm:flex-col', 'max-sm:items-stretch', 'max-sm:w-full']))
    expect(cell.find('span').text()).toBe('Description')
    expect(cell.find('input').exists()).toBe(true)
  })

  it('makes a checkbox header of an editable table the select column, so select-all stays on a phone', async () => {
    const wrapper = mount(tableOf(
      [{ caption: '', props: { select: true } }, { caption: 'Description', props: { cardRole: UiCardRole.Control } }],
      () => [
        h(UiTableCell, { select: true }, { default: () => h('input', { type: 'checkbox' }) }),
        h(UiTableCell, null, { default: () => h('textarea', { placeholder: 'x' }) }),
      ],
    ), { global: global() })
    await flushPromises()
    const cells = wrapper.findAll('[data-testid="row"] td')

    expect(wrapper.find('thead th').classes()).not.toContain('max-sm:hidden')
    expect(cells[0]!.classes()).toContain('max-sm:order-1')
    expect(cells[0]!.find('span').exists()).toBe(false)
    expect(cells[1]!.find('span').text()).toBe('Description')
  })

  it('counts cells at render time: a cell behind a false v-if does not shift the others', async () => {
    /** Whether the middle cell of the row is rendered. */
    const shown = ref(false)
    const wrapper = mount(defineComponent({
      setup: () => () => h(UiTable, null, { default: () => [
        h(UiTableHead, null, { default: () => h(UiTableRow, null, { default: () => [
          h(UiTableHeader, null, { default: () => 'A' }),
          h(UiTableHeader, null, { default: () => 'B' }),
          h(UiTableHeader, null, { default: () => 'C' }),
        ] }) }),
        h(UiTableBody, null, { default: () => h(UiTableRow, { 'data-testid': 'row' }, { default: () => [
          h(UiTableCell, null, { default: () => 'a' }),
          shown.value ? h(UiTableCell, null, { default: () => 'b' }) : null,
          h(UiTableCell, null, { default: () => 'c' }),
        ] }) }),
      ] }),
    }), { global: global() })
    await flushPromises()

    expect(wrapper.findAll('[data-testid="row"] td').map((td) => td.find('span').text())).toEqual(['A', 'B'])

    shown.value = true
    await flushPromises()

    expect(wrapper.findAll('[data-testid="row"] td').map((td) => td.find('span').text())).toEqual(['A', 'B', 'C'])
  })

  it('counts a cell through a fragment of a list and a cell that spans columns as a full width line', async () => {
    const wrapper = mount(tableOf(
      [{ caption: 'A' }, { caption: 'B' }, { caption: 'C' }],
      () => [
        h(UiTableCell, { colspan: 2 }, { default: () => 'span' }),
        [h(UiTableCell, { key: 1 }, { default: () => 'c' })],
      ],
    ), { global: global() })
    await flushPromises()
    const cells = wrapper.findAll('[data-testid="row"] td')

    expect(cells[0]!.classes()).toEqual(expect.arrayContaining(['max-sm:w-full', 'max-sm:block']))
    expect(cells[0]!.find('span').exists()).toBe(false)
    expect(cells[1]!.find('span').text()).toBe('C')
  })

  it('draws the totals of a foot as a card: a spanning label and the amount under its column', async () => {
    const wrapper = mount(defineComponent({
      render: () => h(UiTable, null, { default: () => [
        h(UiTableHead, null, { default: () => h(UiTableRow, null, { default: () => [
          h(UiTableHeader, null, { default: () => 'Item' }),
          h(UiTableHeader, { numeric: true }, { default: () => 'Amount' }),
        ] }) }),
        h(UiTableFoot, null, { default: () => h(UiTableRow, { 'data-testid': 'foot-row' }, { default: () => [
          h(UiTableCell, { colspan: 1 }, { default: () => 'Total' }),
          h(UiTableCell, { numeric: true }, { default: () => '$9' }),
        ] }) }),
      ] }),
    }), { global: global() })
    await flushPromises()
    const cells = wrapper.findAll('[data-testid="foot-row"] td')

    expect(wrapper.find('tfoot').classes()).toContain('max-sm:block')
    expect(wrapper.find('[data-testid="foot-row"]').classes()).toContain('max-sm:rounded-xl')
    expect(cells[1]!.find('span').text()).toBe('Amount')
    expect(cells[1]!.text()).toContain('$9')
  })

  it('updates the captions of the cards when the language changes', async () => {
    const i18n = createI18n({
      legacy: false,
      locale: 'en',
      fallbackLocale: 'en',
      messages: { en: { ...messages.en, caption: 'Email' }, ru: { ...messages.en, caption: 'Почта' } },
    })
    const wrapper = mount(defineComponent({
      setup: () => {
        const { t } = useI18n()
        return () => h(UiTable, null, { default: () => [
          h(UiTableHead, null, { default: () => h(UiTableRow, null, { default: () => h(UiTableHeader, null, { default: () => t('caption') }) }) }),
          h(UiTableBody, null, { default: () => h(UiTableRow, { 'data-testid': 'row' }, { default: () => h(UiTableCell, null, { default: () => 'x' }) }) }),
        ] })
      },
    }), { global: { plugins: [i18n] } })
    await flushPromises()

    expect(wrapper.find('[data-testid="row"] td span').text()).toBe('Email')

    i18n.global.locale.value = 'ru'
    await flushPromises()

    expect(wrapper.find('[data-testid="row"] td span').text()).toBe('Почта')
  })
})

describe('UiTable sort control on a phone', () => {
  /**
   * Reads what the sort control's select was given.
   *
   * @param wrapper The mounted table.
   * @returns The picked position (null for none) and the options offered.
   */
  const sortSelect = (wrapper: VueWrapper): { modelValue: number | null; options: Array<{ label: string }> } =>
    wrapper.findComponent(UiSelect).props() as unknown as { modelValue: number | null; options: Array<{ label: string }> }

  /**
   * A table with two sortable columns and one that is not.
   *
   * @param onSort Called with the caption of the column whose sort was requested.
   * @param direction Current order of the Name column.
   * @returns A component to mount.
   */
  const sortableHarness = (onSort: (caption: string) => void, direction: UiSortDirection = UiSortDirection.None) => defineComponent({
    render: () => h(UiTable, null, { default: () => [
      h(UiTableHead, null, { default: () => h(UiTableRow, null, { default: () => [
        h(UiTableHeader, { sortable: true, direction, onSort: () => onSort('Name') }, { default: () => 'Name' }),
        h(UiTableHeader, { sortable: true, onSort: () => onSort('Date') }, { default: () => 'Date' }),
        h(UiTableHeader, null, { default: () => 'Email' }),
      ] }) }),
      h(UiTableBody, null, { default: () => h(UiTableRow, null, { default: () => [
        h(UiTableCell, null, { default: () => 'a' }),
        h(UiTableCell, null, { default: () => 'b' }),
        h(UiTableCell, null, { default: () => 'c' }),
      ] }) }),
    ] }),
  })

  it('offers the sortable columns above the cards, on phones only', async () => {
    const wrapper = mount(sortableHarness(vi.fn()), { global: global() })
    await flushPromises()
    expect(wrapper.find('[data-testid="ui-table-sort"]').classes()).toContain('sm:hidden')
    expect(sortSelect(wrapper).options.map((option) => option.label)).toEqual(['Name', 'Date'])
  })

  it('has no sort control while no header is sortable', async () => {
    const wrapper = mount(cardHarness(), { global: global() })
    await flushPromises()

    expect(wrapper.find('[data-testid="ui-table-sort"]').exists()).toBe(false)
  })

  it('asks the screen to sort by the picked column through the header sort request', async () => {
    const onSort = vi.fn()
    const wrapper = mount(sortableHarness(onSort), { global: global() })
    await flushPromises()

    wrapper.findComponent(UiSelect).vm.$emit('update:modelValue', 1)
    await nextTick()

    expect(onSort).toHaveBeenCalledExactlyOnceWith('Date')
  })

  it('shows the current order and reverses it through the direction button', async () => {
    const onSort = vi.fn()
    const wrapper = mount(sortableHarness(onSort, UiSortDirection.Descending), { global: global() })
    await flushPromises()

    expect(sortSelect(wrapper).modelValue).toBe(0)
    expect(wrapper.get('[data-testid="ui-table-sort-direction"]').attributes('disabled')).toBeUndefined()

    await wrapper.get('[data-testid="ui-table-sort-direction"]').trigger('click')

    expect(onSort).toHaveBeenCalledExactlyOnceWith('Name')
  })

  it('disables the direction button while no column is the order', async () => {
    const wrapper = mount(sortableHarness(vi.fn()), { global: global() })
    await flushPromises()

    expect(sortSelect(wrapper).modelValue).toBeNull()
    expect(wrapper.get('[data-testid="ui-table-sort-direction"]').attributes('disabled')).toBeDefined()
  })
})
