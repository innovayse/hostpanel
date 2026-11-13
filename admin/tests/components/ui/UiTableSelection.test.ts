/**
 * Unit tests for the selectable table: the checkbox column drawn by UiTableRow, the select-all state, row toggling
 * with a Shift range, the selected look and aria-selected.
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
import { useRowSelection } from '@/composables/useRowSelection'
import { createTestI18n } from '../../helpers/i18n'

/** The ids of the rows the harness shows. */
const ROW_IDS = [1, 2, 3, 4]

/**
 * A selectable table of four rows over one selection.
 *
 * @returns The component and the selection it uses.
 */
const harness = () => {
  const selection = useRowSelection(() => ROW_IDS)
  const Harness = defineComponent({
    render: () => h(UiTable, { selection, selectAllTestid: 'all' }, {
      default: () => [
        h(UiTableHead, null, { default: () => h(UiTableRow, null, { default: () => h(UiTableHeader, null, { default: () => 'Name' }) }) }),
        h(UiTableBody, null, { default: () => ROW_IDS.map((id) => h(
          UiTableRow,
          { key: id, rowId: id, selectTestid: `pick-${id}`, 'data-testid': `row-${id}` },
          { default: () => h(UiTableCell, null, { default: () => `Row ${id}` }) },
        )) }),
      ],
    }),
  })
  return { selection, wrapper: mount(Harness, { global: { plugins: [createTestI18n()] } }) }
}

describe('selectable UiTable', () => {
  it('draws a select-all header cell and a checkbox per row, named for assistive tech', () => {
    const { wrapper } = harness()

    expect(wrapper.findAll('th')).toHaveLength(2)
    expect(wrapper.get('[data-testid="all"]').attributes('aria-label')).toBe('Select All')
    expect(wrapper.findAll('[role="checkbox"]')).toHaveLength(5)
    expect(wrapper.get('[data-testid="pick-1"]').attributes('aria-label')).toBe('Select row')
  })

  it('draws no checkbox column without a selection', () => {
    const wrapper = mount(UiTable, {
      slots: { default: '<tbody><tr><td>x</td></tr></tbody>' },
      global: { plugins: [createTestI18n()] },
    })

    expect(wrapper.find('[role="checkbox"]').exists()).toBe(false)
  })

  it('draws no row checkbox for a row without an id', () => {
    const selection = useRowSelection(() => ROW_IDS)
    const Plain = defineComponent({
      render: () => h(UiTable, { selection }, {
        default: () => h(UiTableBody, null, { default: () => h(UiTableRow, null, { default: () => h(UiTableCell, null, { default: () => 'total' }) }) }),
      }),
    })

    expect(mount(Plain, { global: { plugins: [createTestI18n()] } }).find('[role="checkbox"]').exists()).toBe(false)
  })

  it('ticks a row on click and marks it selected with the accent look and aria-selected', async () => {
    const { wrapper, selection } = harness()

    await wrapper.get('[data-testid="pick-2"]').trigger('click')

    expect(selection.ids.value).toEqual([2])
    const row = wrapper.get('[data-testid="row-2"]')
    expect(row.attributes('aria-selected')).toBe('true')
    expect(row.classes()).toEqual(expect.arrayContaining(['bg-primary-500/10']))
    expect(row.classes().join(' ')).toContain('shadow-[inset_3px_0_0_var(--text-accent)]')
    expect(wrapper.get('[data-testid="row-1"]').attributes('aria-selected')).toBe('false')
    expect(wrapper.get('[data-testid="pick-2"]').attributes('aria-checked')).toBe('true')
  })

  it('extends the selection to a range on a Shift click', async () => {
    const { wrapper, selection } = harness()

    await wrapper.get('[data-testid="pick-1"]').trigger('click')
    await wrapper.get('[data-testid="pick-3"]').trigger('click', { shiftKey: true })

    expect([...selection.ids.value].sort()).toEqual([1, 2, 3])
  })

  it('shows the select-all box indeterminate over a partial selection and checked over a full one', async () => {
    const { wrapper } = harness()
    const all = wrapper.get('[data-testid="all"]')
    expect(all.attributes('aria-checked')).toBe('false')

    await wrapper.get('[data-testid="pick-1"]').trigger('click')
    expect(all.attributes('aria-checked')).toBe('mixed')

    await all.trigger('click')
    expect(all.attributes('aria-checked')).toBe('true')
    expect(wrapper.findAll('tbody tr[aria-selected="true"]')).toHaveLength(4)

    await all.trigger('click')
    expect(wrapper.findAll('tbody tr[aria-selected="true"]')).toHaveLength(0)
  })

  it('gives the checkbox column its narrow shape', () => {
    const { wrapper } = harness()

    expect(wrapper.get('th').classes()).toEqual(expect.arrayContaining(['w-12', 'select-none']))
    expect(wrapper.get('tbody td').classes()).toEqual(expect.arrayContaining(['w-12', 'select-none']))
  })
})
