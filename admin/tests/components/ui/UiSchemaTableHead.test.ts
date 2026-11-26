/**
 * Unit tests for UiSchemaTableHead: one header per schema column, with the caption and the breakpoint the column names, and
 * the table's own "Actions" caption for the actions column.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import { createTestI18n } from '../../helpers/i18n'
import type { SkeletonColumn } from '@/utils/ui/skeletonSchema'
import { UI_TABLE_CONTEXT, UiTableDensity } from '@/utils/ui/injection'
import { UiColumnSize, UiHideBelow } from '@/utils/ui/table'

/** The columns the tests draw: a plain one, a hidden one, a number one and the actions one. */
const COLUMNS: readonly SkeletonColumn[] = [
  { key: 'name', label: 'shared.name', size: UiColumnSize.Grow },
  { key: 'email', label: 'shared.email', hideBelow: UiHideBelow.Lg },
  { key: 'amount', label: 'shared.id', numeric: true },
  { key: 'actions', actions: true },
]

describe('UiSchemaTableHead', () => {
  it('draws one header per column with the caption of its locale key', () => {
    const wrapper = mount(UiSchemaTableHead, { props: { columns: COLUMNS }, global: { plugins: [createTestI18n()] } })
    const headers = wrapper.findAll('th')

    expect(headers).toHaveLength(COLUMNS.length)
    expect(headers[0]?.text()).toBe('Name')
    expect(headers[1]?.text()).toBe('Email')
  })

  it('hides a column below the breakpoint its schema names', () => {
    const wrapper = mount(UiSchemaTableHead, { props: { columns: COLUMNS }, global: { plugins: [createTestI18n()] } })

    expect(wrapper.findAll('th')[1]?.classes()).toEqual(expect.arrayContaining(['hidden', 'lg:table-cell']))
  })

  it('names the actions column "Actions" without the schema naming it, as part of the table', () => {
    const wrapper = mount(UiSchemaTableHead, {
      props: { columns: COLUMNS },
      global: { plugins: [createTestI18n()], provide: { [UI_TABLE_CONTEXT as symbol]: { density: UiTableDensity.Comfortable, actionsLabel: 'Actions' } } },
    })

    expect(wrapper.findAll('th')[3]?.text()).toBe('Actions')
  })
})
