/**
 * Unit tests for UiTableHeader: native th, caption typography, alignment and sorting.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'

describe('UiTableHeader', () => {
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

  it('aligns on request', () => {
    expect(mount(UiTableHeader, { props: { align: 'center' } }).classes()).toContain('text-center')
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

  it('keeps a readable minimum width that a caller can override', () => {
    expect(mount(UiTableHeader).classes()).toContain('[:where(&)]:min-w-28')
  })
})
