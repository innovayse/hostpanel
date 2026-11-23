/**
 * Unit tests for UiTable: the framed shell around a real table and the scroll option.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiTable from '~/components/ui/UiTable.vue'

describe('UiTable', () => {
  it('frames a table that holds the slot', () => {
    const wrapper = mountWithKit(UiTable, { slots: { default: '<tbody><tr><td>cell</td></tr></tbody>' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['rounded-2xl', 'border', 'overflow-hidden']))
    expect(wrapper.find('table td').text()).toBe('cell')
  })

  it('scrolls sideways only when scrollable', () => {
    const scrollable = mountWithKit(UiTable, { props: { scrollable: true } })
    const fixed = mountWithKit(UiTable)

    expect(scrollable.find('.overflow-x-auto').exists()).toBe(true)
    expect(fixed.find('.overflow-x-auto').exists()).toBe(false)
  })

  it('passes data-testid to the frame', () => {
    const wrapper = mountWithKit(UiTable, { attrs: { 'data-testid': 'invoices-table' } })

    expect(wrapper.attributes('data-testid')).toBe('invoices-table')
  })

  it('makes a labelled scroll area a focus stop', () => {
    const scroller = mountWithKit(UiTable, { props: { scrollable: true, label: 'Plans' } }).find('.overflow-x-auto')

    expect(scroller.attributes('tabindex')).toBe('0')
    expect(scroller.attributes('role')).toBe('region')
    expect(scroller.attributes('aria-label')).toBe('Plans')
  })

  it('leaves the scroll area out of the tab order without a label', () => {
    const scroller = mountWithKit(UiTable, { props: { scrollable: true } }).find('.overflow-x-auto')

    expect(scroller.attributes('tabindex')).toBeUndefined()
  })

  it('keeps the frame unless flush', () => {
    const framed = mountWithKit(UiTable)
    const flush = mountWithKit(UiTable, { props: { flush: true } })

    expect(framed.classes()).toEqual(expect.arrayContaining(['border', 'rounded-2xl', 'bg-ui-surface']))
    expect(flush.classes()).toContain('overflow-hidden')
    expect(flush.classes()).not.toContain('border')
    expect(flush.classes()).not.toContain('rounded-2xl')
    expect(flush.classes()).not.toContain('bg-ui-surface')
  })

  it('draws the head and the body slots', () => {
    const wrapper = mountWithKit(UiTable, {
      slots: { head: '<thead><tr><th>h</th></tr></thead>', default: '<tbody><tr><td>b</td></tr></tbody>' },
    })

    expect(wrapper.find('thead th').text()).toBe('h')
    expect(wrapper.find('tbody td').text()).toBe('b')
  })

  it('draws one placeholder cell per column per loading row instead of the body', () => {
    const wrapper = mountWithKit(UiTable, {
      props: { loading: true, columns: 3 },
      slots: { default: '<tbody data-testid="rows"><tr><td>b</td></tr></tbody>' },
    })

    expect(wrapper.find('[data-testid="rows"]').exists()).toBe(false)
    const rows = wrapper.findAll('[data-testid="ui-table-loading"] tr')
    expect(rows.length).toBeGreaterThan(0)
    expect(rows[0]?.findAll('td')).toHaveLength(3)
    expect(rows[0]?.findAll('[data-testid="ui-skeleton"]')).toHaveLength(3)
  })

  it('draws the empty state in one full-width row instead of the body', () => {
    const wrapper = mountWithKit(UiTable, {
      props: { empty: true, emptyText: 'Nothing here', emptyDescription: 'Add one', columns: 4 },
      slots: { default: '<tbody data-testid="rows"><tr><td>b</td></tr></tbody>' },
    })

    expect(wrapper.find('[data-testid="rows"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="ui-table-empty"] td').attributes('colspan')).toBe('4')
    expect(wrapper.find('[data-testid="ui-empty-state"]').text()).toContain('Nothing here')
    expect(wrapper.find('[data-testid="ui-empty-state"]').text()).toContain('Add one')
  })

  it('puts the empty slot in the empty state as its action', () => {
    const wrapper = mountWithKit(UiTable, {
      props: { empty: true, emptyText: 'Nothing here' },
      slots: { empty: '<button data-testid="first">New</button>' },
    })

    expect(wrapper.find('[data-testid="ui-empty-state"] [data-testid="first"]').exists()).toBe(true)
  })

  it('shows the loading state before the empty state', () => {
    const wrapper = mountWithKit(UiTable, { props: { loading: true, empty: true, emptyText: 'Nothing here' } })

    expect(wrapper.find('[data-testid="ui-table-loading"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="ui-table-empty"]').exists()).toBe(false)
  })
})
