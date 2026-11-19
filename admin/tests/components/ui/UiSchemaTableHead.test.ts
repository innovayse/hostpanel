/**
 * Unit tests for UiSchemaTableHead: one header per schema column, with the caption, the breakpoint and the actions caption
 * the column names.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import { createTestI18n } from '../../helpers/i18n'
import type { SkeletonColumn } from '@/utils/ui/skeletonSchema'
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

  it('draws the caption an actions column names, for assistive technology only', () => {
    const wrapper = mount(UiSchemaTableHead, {
      props: { columns: [{ key: 'actions', label: 'shared.actions', actions: true }] },
      global: { plugins: [createTestI18n()] },
    })

    expect(wrapper.find('th').text()).toBe('Actions')
    expect(wrapper.find('th .sr-only').text()).toBe('Actions')
  })

  it('gives an actions column with no caption the screen reader caption', () => {
    const wrapper = mount(UiSchemaTableHead, { props: { columns: COLUMNS }, global: { plugins: [createTestI18n()] } })
    const caption = wrapper.findAll('th')[3]?.find('.sr-only')

    expect(caption?.text()).toBe('Actions')
  })
})
