/**
 * Unit tests for the phone render of UiTable: below `md` the table is a UiCardList of UiRecordCards built from the same header columns and row
 * slots, each cell lands in the part of the card its column's role names, and no table markup is drawn.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h, nextTick, ref } from 'vue'
import type { VNodeChild } from 'vue'
import { createI18n, useI18n } from 'vue-i18n'
import { flushPromises, mount } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import UiIcon from '@/components/ui/UiIcon.vue'
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
import { UI_BELOW_MD_QUERY } from '@/utils/ui/breakpoints'
import { UiCardRole, UiSortDirection } from '@/utils/ui/table'
import { createTestI18n } from '../../helpers/i18n'

/** Global options giving the table its i18n plugin. */
const global = () => ({ plugins: [createTestI18n()] })

/** The `matchMedia` of the test document before a test replaced it. */
const originalMatchMedia = window.matchMedia

beforeEach(() => {
  // A phone: every media query of the table (below `md`) matches.
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    writable: true,
    value: (query: string) => ({ matches: true, media: query, addEventListener: () => undefined, removeEventListener: () => undefined }),
  })
})

afterEach(() => {
  Object.defineProperty(window, 'matchMedia', { configurable: true, writable: true, value: originalMatchMedia })
})

/**
 * A table with a heading, a field, a badge, a left out and an actions column and one row.
 *
 * @param tableProps Props of the table.
 * @param rowProps Props of the body row.
 * @returns A component to mount.
 */
const cardHarness = (tableProps: Record<string, unknown> = {}, rowProps: Record<string, unknown> = {}) => defineComponent({
  render: () => h(UiTable, tableProps, {
    default: () => [
      h(UiTableHead, null, { default: () => h(UiTableRow, null, { default: () => [
        h(UiTableHeader, null, { default: () => 'Name' }),
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
        h(UiTableCell, { actions: true }, { default: () => h('button', { 'data-testid': 'more' }, 'more') }),
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

describe('UiTable as a card list', () => {
  it('is a list of cards from the md breakpoint down and a table above it', async () => {
    /** Makes `matchMedia` report a match for the given query only. */
    const stub = (matching: string): void => {
      Object.defineProperty(window, 'matchMedia', {
        configurable: true,
        writable: true,
        value: (query: string) => ({ matches: query === matching, media: query, addEventListener: () => undefined, removeEventListener: () => undefined }),
      })
    }

    stub(UI_BELOW_MD_QUERY)
    const tablet = mount(cardHarness(), { global: global() })
    await nextTick()
    expect(tablet.find('table').exists()).toBe(false)
    expect(tablet.text()).toContain('Ada')

    stub('')
    const desktop = mount(cardHarness(), { global: global() })
    await nextTick()
    expect(desktop.find('table').exists()).toBe(true)
  })

  it('draws a list of record cards and no table markup', async () => {
    const wrapper = mount(cardHarness(), { global: global() })
    await flushPromises()

    expect(wrapper.find('table').exists()).toBe(false)
    expect(wrapper.find('th').exists()).toBe(false)
    expect(wrapper.find('td').exists()).toBe(false)
    expect(wrapper.find('[role="list"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="row"]').attributes('role')).toBe('listitem')
  })

  it('puts every cell in the part of the card its column plays, the first column being the heading by default', async () => {
    const wrapper = mount(cardHarness(), { global: global() })
    await flushPromises()
    const card = wrapper.get('[data-testid="row"]')

    expect(card.get('.font-semibold').text()).toContain('Ada')
    expect(card.text()).toContain('Email')
    expect(card.text()).toContain('ada@x.io')
    expect(card.text()).toContain('Active')
    expect(card.find('[data-testid="more"]').exists()).toBe(true)
  })

  it('leaves a column of the hidden role out and captions a field with real text', async () => {
    const wrapper = mount(cardHarness(), { global: global() })
    await flushPromises()
    const card = wrapper.get('[data-testid="row"]')

    expect(card.text()).not.toContain('7')
    const caption = card.findAll('span').find((span) => span.text() === 'Email')
    expect(caption?.classes()).toContain('text-xs')
  })

  it('picks the growing column as the heading when no header names one', async () => {
    const wrapper = mount(tableOf(
      [{ caption: 'Id' }, { caption: 'Name', props: { size: 'grow' } }],
      () => [h(UiTableCell, null, { default: () => '7' }), h(UiTableCell, null, { default: () => 'Ada' })],
    ), { global: global() })
    await flushPromises()
    const card = wrapper.get('[data-testid="row"]')

    expect(card.get('.font-semibold').text()).toContain('Ada')
    expect(card.text()).toContain('Id')
  })

  it('draws a subtitle as a muted line under the heading', async () => {
    const wrapper = mount(tableOf(
      [{ caption: 'Name' }, { caption: 'Email', props: { cardRole: UiCardRole.Subtitle } }],
      () => [h(UiTableCell, null, { default: () => 'Ada' }), h(UiTableCell, null, { default: () => 'ada@x.io' })],
    ), { global: global() })
    await flushPromises()

    expect(wrapper.get('[data-testid="row"] .text-text-muted').text()).toContain('ada@x.io')
  })

  it('shows a column that is hidden below a breakpoint in the wide table as a field of the card', async () => {
    const wrapper = mount(tableOf(
      [{ caption: 'Name' }, { caption: 'Email', props: { hideBelow: 'lg' } }],
      () => [h(UiTableCell, null, { default: () => 'Ada' }), h(UiTableCell, { hideBelow: 'lg' }, { default: () => 'ada@x.io' })],
    ), { global: global() })
    await flushPromises()

    expect(wrapper.get('[data-testid="row"]').text()).toContain('ada@x.io')
  })

  it('draws loading placeholders as record cards in a list', async () => {
    const wrapper = mount(cardHarness({ loading: true, skeletonRows: 3 }), { global: global() })
    await flushPromises()

    expect(wrapper.get('[data-testid="ui-table-skeleton"]').attributes('role')).toBe('list')
    expect(wrapper.findAll('[data-testid="ui-table-skeleton"] [role="listitem"]')).toHaveLength(3)
    expect(wrapper.findAll('[data-testid="ui-record-card-skeleton"]')).toHaveLength(3)
    expect(wrapper.find('table').exists()).toBe(false)
  })

  it('draws the empty state in place of the cards', async () => {
    const wrapper = mount(cardHarness({ empty: true, emptyText: 'Nothing here' }), { global: global() })
    await flushPromises()

    expect(wrapper.get('[data-testid="ui-table-empty"]').text()).toContain('Nothing here')
  })

  it('keeps the checkbox on the card and puts select-all in the list header', async () => {
    const selection = useRowSelection(() => [1])
    const wrapper = mount(cardHarness({ selection }, { rowId: 1 }), { global: global() })
    await flushPromises()

    expect(wrapper.find('[data-testid="row"] [data-testid="ui-table-select-row"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="ui-card-list-header"] [data-testid="ui-table-select-all"]').exists()).toBe(true)
  })

  it('marks a ticked card selected', async () => {
    const selection = useRowSelection(() => [1])
    selection.toggle(1, false)
    const wrapper = mount(cardHarness({ selection }, { rowId: 1 }), { global: global() })
    await flushPromises()

    expect(wrapper.get('[data-testid="row"]').classes()).toContain('ring-1')
  })

  it('reads the caption of a sortable header without its sort arrow', async () => {
    const wrapper = mount(tableOf([{ caption: 'Name' }, { caption: 'Date', props: { sortable: true } }], () => [
      h(UiTableCell, null, { default: () => 'x' }),
      h(UiTableCell, null, { default: () => 'y' }),
    ]), { global: global() })
    await flushPromises()

    expect(wrapper.get('[data-testid="row"]').text()).toContain('Date')
    expect(wrapper.get('[data-testid="row"]').findComponent(UiIcon).exists()).toBe(false)
  })

  it('takes the card role of an editable column: the caption over a full width control', async () => {
    const wrapper = mount(tableOf(
      [{ caption: 'Description', props: { cardRole: UiCardRole.Control } }],
      () => [h(UiTableCell, null, { default: () => h('input', { placeholder: 'x' }) })],
    ), { global: global() })
    await flushPromises()
    const card = wrapper.get('[data-testid="row"]')

    expect(card.text()).toContain('Description')
    expect(card.find('input').exists()).toBe(true)
    expect(card.find('.flex-col.items-stretch').exists()).toBe(true)
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

    expect(wrapper.get('[data-testid="row"]').text()).toContain('B')
    expect(wrapper.get('[data-testid="row"]').text()).not.toContain('C')

    shown.value = true
    await flushPromises()

    expect(wrapper.get('[data-testid="row"]').text()).toContain('C')
  })

  it('draws a cell that spans columns as a full width line of the card', async () => {
    const wrapper = mount(tableOf(
      [{ caption: 'A' }, { caption: 'B' }, { caption: 'C' }],
      () => [
        h(UiTableCell, { colspan: 2 }, { default: () => 'span' }),
        [h(UiTableCell, { key: 1 }, { default: () => 'c' })],
      ],
    ), { global: global() })
    await flushPromises()
    const card = wrapper.get('[data-testid="row"]')

    expect(card.get('.w-full').text()).toContain('span')
    expect(card.text()).toContain('C')
  })

  it('draws the totals of a foot as a card', async () => {
    const wrapper = mount(defineComponent({
      render: () => h(UiTable, null, { default: () => [
        h(UiTableHead, null, { default: () => h(UiTableRow, null, { default: () => [
          h(UiTableHeader, null, { default: () => 'Item' }),
          h(UiTableHeader, { numeric: true }, { default: () => 'Amount' }),
        ] }) }),
        h(UiTableFoot, null, { default: () => h(UiTableRow, { 'data-testid': 'foot-row' }, { default: () => [
          h(UiTableCell, null, { default: () => 'Total' }),
          h(UiTableCell, { numeric: true }, { default: () => '$9' }),
        ] }) }),
      ] }),
    }), { global: global() })
    await flushPromises()

    expect(wrapper.find('tfoot').exists()).toBe(false)
    expect(wrapper.get('[data-testid="foot-row"]').attributes('role')).toBe('listitem')
    expect(wrapper.get('[data-testid="foot-row"]').text()).toContain('$9')
  })

  it('updates the captions of the cards when the language changes', async () => {
    const i18n = createI18n({
      legacy: false,
      locale: 'en',
      fallbackLocale: 'en',
      messages: { en: { ...messages.en, caption: 'Email' }, ru: { ...messages.en, caption: 'Mail ru' } },
    })
    const wrapper = mount(defineComponent({
      setup: () => {
        const { t } = useI18n()
        return () => h(UiTable, null, { default: () => [
          h(UiTableHead, null, { default: () => h(UiTableRow, null, { default: () => [
            h(UiTableHeader, null, { default: () => 'Name' }),
            h(UiTableHeader, null, { default: () => t('caption') }),
          ] }) }),
          h(UiTableBody, null, { default: () => h(UiTableRow, { 'data-testid': 'row' }, { default: () => [
            h(UiTableCell, null, { default: () => 'x' }),
            h(UiTableCell, null, { default: () => 'y' }),
          ] }) }),
        ] })
      },
    }), { global: { plugins: [i18n] } })
    await flushPromises()

    expect(wrapper.get('[data-testid="row"]').text()).toContain('Email')

    i18n.global.locale.value = 'ru'
    await flushPromises()

    expect(wrapper.get('[data-testid="row"]').text()).toContain('Mail ru')
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

  it('offers the sortable columns above the cards', async () => {
    const wrapper = mount(sortableHarness(vi.fn()), { global: global() })
    await flushPromises()

    expect(wrapper.find('[data-testid="ui-card-list-header"]').exists()).toBe(true)
    expect(sortSelect(wrapper).options.map((option) => option.label)).toEqual(['Name', 'Date'])
  })

  it('has no sort control while no header is sortable', async () => {
    const wrapper = mount(cardHarness(), { global: global() })
    await flushPromises()

    expect(wrapper.find('[data-testid="ui-table-sort-column"]').exists()).toBe(false)
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
