/**
 * Unit tests for UiSkeletonTable: a schema table as its real header captions over placeholder rows, with an optional pager.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import UiSkeletonTable from '@/components/ui/UiSkeletonTable.vue'
import { createTestI18n } from '../../helpers/i18n'
import type { SkeletonTable } from '@/utils/ui/skeletonSchema'
import { UiColumnSize } from '@/utils/ui/table'

/** A table of three columns, the last right aligned and without a caption. */
const table: SkeletonTable = {
  columns: [{ key: 'id', label: 'shared.id' }, { key: 'name', label: 'shared.name' }, { key: 'actions', align: 'right' }],
}

/**
 * Mounts the skeleton with the real catalogue.
 *
 * @param value - The table of the schema.
 * @returns The mounted wrapper.
 */
const mountTable = (value: SkeletonTable) => mount(UiSkeletonTable, { props: { table: value }, global: { plugins: [createTestI18n()] } })

describe('UiSkeletonTable', () => {
  it('draws the real captions of the columns, and an empty header for a column with none', () => {
    const wrapper = mountTable(table)

    expect(wrapper.findAll('[role="columnheader"]').map((header) => header.text())).toEqual(['ID', 'Name', ''])
  })

  it('draws the default number of placeholder rows, one bar per column', async () => {
    const wrapper = mountTable(table)
    await nextTick()

    expect(wrapper.findAll('[data-testid="ui-table-skeleton"] [role="row"]')).toHaveLength(5)
    expect(wrapper.findAll('[data-testid="ui-table-skeleton"] [role="row"]:first-child [data-testid="ui-skeleton-bar"]')).toHaveLength(3)
  })

  it('draws as many rows as the schema says', () => {
    const wrapper = mountTable({ ...table, rows: 2 })

    expect(wrapper.findAll('[data-testid="ui-table-skeleton"] [role="row"]')).toHaveLength(2)
  })

  it('gives a column the width the schema names', () => {
    const wrapper = mountTable({ columns: [{ key: 'id', label: 'shared.id', size: UiColumnSize.Compact }, { key: 'name', label: 'shared.name', size: UiColumnSize.Grow }] })
    const [compact, grow] = wrapper.findAll('[role="columnheader"]')

    expect(compact?.classes()).toContain('w-px')
    expect(grow?.classes()).toContain('w-full')
  })

  it('opens the rows with a group heading placeholder only for a grouped table', () => {
    expect(mountTable({ ...table, grouped: true }).find('[data-testid="ui-table-skeleton-group"]').exists()).toBe(true)
    expect(mountTable(table).find('[data-testid="ui-table-skeleton-group"]').exists()).toBe(false)
  })

  it('draws the pager under the table only when the schema has one', () => {
    expect(mountTable(table).find('[data-testid="pagination-range"]').exists()).toBe(false)
    expect(mountTable({ ...table, pagination: true }).find('[data-testid="pagination-range"]').exists()).toBe(true)
  })
})
