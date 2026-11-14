/**
 * Unit tests for UiTableHeader: native th, caption typography, alignment and sorting.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import { UiCardRole } from '@/utils/ui/table'

describe('UiTableHeader', () => {
  it('hides the column below its breakpoint and shows it as a table cell from there', () => {
    const wrapper = mount(UiTableHeader, { props: { hideBelow: 'sm' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['hidden', 'sm:table-cell']))
    expect(mount(UiTableHeader).classes()).not.toContain('hidden')
  })

  it('renders the native th with its slot', () => {
    const wrapper = mount(UiTableHeader, { slots: { default: 'Name' } })

    expect(wrapper.element.tagName).toBe('TH')
    expect(wrapper.text()).toBe('Name')
  })

  it('passes classes and data attributes through', () => {
    const wrapper = mount(UiTableHeader, { attrs: { class: 'x-class', 'data-testid': 'probe' } })

    expect(wrapper.classes()).toContain('x-class')
    expect(wrapper.attributes('data-testid')).toBe('probe')
  })

  it('paints the subtle header hairline itself and stays transparent over the card', () => {
    const classes = mount(UiTableHeader).classes()

    expect(classes).toContain('shadow-[inset_0_-1px_0_var(--border-subtle)]')
    expect(classes.some(name => name.startsWith('bg-'))).toBe(false)
  })

  it('aligns on request', () => {
    expect(mount(UiTableHeader, { props: { align: 'center' } }).classes()).toContain('text-center')
  })

  it('matches the numeric and actions cells below it', () => {
    expect(mount(UiTableHeader, { props: { numeric: true } }).classes()).toEqual(expect.arrayContaining(['text-end', 'tabular-nums']))
    expect(mount(UiTableHeader, { props: { actions: true } }).classes()).toEqual(expect.arrayContaining(['w-16', 'sm:w-28', 'text-end']))
  })

  it('is a column header for assistive technology whatever the layout', () => {
    expect(mount(UiTableHeader).attributes('role')).toBe('columnheader')
  })

  it('keeps its caption in a display contents span, so the caption can be read without the sort arrow', () => {
    const plain = mount(UiTableHeader, { slots: { default: 'Name' } })
    const sortable = mount(UiTableHeader, { props: { sortable: true }, slots: { default: 'Name' } })

    expect(plain.get('span').classes()).toContain('contents')
    expect(plain.get('span').text()).toBe('Name')
    expect(sortable.get('button span.contents').text()).toBe('Name')
  })

  it('is not drawn on a card, except the select column that holds select-all', () => {
    expect(mount(UiTableHeader).classes()).toContain('max-sm:hidden')
    expect(mount(UiTableHeader, { props: { select: true } }).classes()).not.toContain('max-sm:hidden')
  })

  it('takes a card role without changing its own look', () => {
    const wrapper = mount(UiTableHeader, { props: { cardRole: UiCardRole.Title }, slots: { default: 'Name' } })

    expect(wrapper.text()).toBe('Name')
    expect(wrapper.classes()).toContain('max-sm:hidden')
  })

  it('has no sort button or aria-sort unless sortable', () => {
    const wrapper = mount(UiTableHeader)

    expect(wrapper.find('button').exists()).toBe(false)
    expect(wrapper.attributes('aria-sort')).toBeUndefined()
  })

  it('turns the caption into a sort button that reports the order', async () => {
    const wrapper = mount(UiTableHeader, {
      props: { sortable: true, direction: 'ascending', sortTestid: 'sort-name' },
      slots: { default: 'Name' },
    })

    expect(wrapper.attributes('aria-sort')).toBe('ascending')
    expect(wrapper.get('button').text()).toContain('Name')
    expect(wrapper.get('button').text()).toContain('▲')

    await wrapper.get('[data-testid="sort-name"]').trigger('click')

    expect(wrapper.emitted('sort')).toHaveLength(1)
  })

  it.each([
    ['none', '↕'],
    ['descending', '▼'],
  ] as const)('shows the arrow for %s', (direction, arrow) => {
    const wrapper = mount(UiTableHeader, { props: { sortable: true, direction } })

    expect(wrapper.get('button').text()).toContain(arrow)
  })

  it('gives the sort button a visible keyboard focus outline', () => {
    const wrapper = mount(UiTableHeader, { props: { sortable: true } })

    expect(wrapper.get('button').classes()).toContain('focus-visible:outline-primary-500')
  })
})
