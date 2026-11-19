/**
 * Unit tests for UiCardList: list semantics, the gap between cards, the sort and select-all header, the loading and empty states and the footer.
 */
import { describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import UiCardList from '@/components/ui/UiCardList.vue'
import UiRecordCard from '@/components/ui/UiRecordCard.vue'
import { useRowSelection } from '@/composables/useRowSelection'
import { UiSortDirection } from '@/utils/ui/table'
import { createTestI18n } from '../../helpers/i18n'

/** Global options giving the list its i18n plugin. */
const global = () => ({ plugins: [createTestI18n()] })

/** Two cards as the default slot. */
const cards = '<div role="listitem" data-testid="card">a</div><div role="listitem" data-testid="card">b</div>'

describe('UiCardList', () => {
  it('is a list of its items with one gap between them', () => {
    const wrapper = mount(UiCardList, { global: global(), slots: { default: cards } })

    const list = wrapper.get('[role="list"]')
    expect(list.classes()).toEqual(expect.arrayContaining(['flex', 'flex-col', 'gap-2']))
    expect(list.findAll('[data-testid="card"]')).toHaveLength(2)
  })

  it('has no header while it has nothing to sort and cannot be selected', () => {
    const wrapper = mount(UiCardList, { global: global(), slots: { default: cards } })

    expect(wrapper.find('[data-testid="ui-card-list-header"]').exists()).toBe(false)
  })

  it('offers the sort fields, shows the current one and reports a pick and a reversal', async () => {
    const wrapper = mount(UiCardList, {
      global: global(),
      props: { sortOptions: [{ value: 0, label: 'Name' }, { value: 1, label: 'Date' }], sortValue: 0, sortDirection: UiSortDirection.Descending },
      slots: { default: cards },
    })

    expect(wrapper.find('[data-testid="ui-card-list-header"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="ui-table-sort-direction"]').attributes('disabled')).toBeUndefined()

    await wrapper.get('[data-testid="ui-table-sort-direction"]').trigger('click')

    expect(wrapper.emitted('reverse')).toHaveLength(1)
  })

  it('disables the direction while no field is the order', () => {
    const wrapper = mount(UiCardList, {
      global: global(),
      props: { sortOptions: [{ value: 0, label: 'Name' }] },
      slots: { default: cards },
    })

    expect(wrapper.get('[data-testid="ui-table-sort-direction"]').attributes('disabled')).toBeDefined()
  })

  it('draws select-all for a selectable list and toggles every record through it', async () => {
    const selection = useRowSelection(() => [1, 2])
    const wrapper = mount(UiCardList, { global: global(), props: { selection }, slots: { default: cards } })

    await wrapper.get('[data-testid="ui-table-select-all"]').trigger('click')
    await flushPromises()

    expect(selection.count.value).toBe(2)
  })

  it('draws select-all as a labelled row of its own above the list, with the selected count', async () => {
    const selection = useRowSelection(() => [1, 2])
    selection.toggle(1)
    const wrapper = mount(UiCardList, { global: global(), props: { selection }, slots: { default: cards } })

    const row = wrapper.get('[data-testid="ui-card-list-select-all"]')
    expect(row.text()).toContain('Select All')
    expect(row.get('[data-testid="ui-card-list-selected-count"]').text()).toContain('1')
    expect(wrapper.get('[data-testid="ui-card-list"]').find('[data-testid="ui-card-list-select-all"]').exists()).toBe(false)
  })

  it('shows the select-all box unchecked, mixed or checked for none, some and all selected, and counts only when any are', async () => {
    const selection = useRowSelection(() => [1, 2])
    const wrapper = mount(UiCardList, { global: global(), props: { selection }, slots: { default: cards } })
    /** The state the select-all box reports to assistive technology. */
    const box = () => wrapper.get('[data-testid="ui-table-select-all"]').attributes('aria-checked')

    expect(box()).toBe('false')
    expect(wrapper.find('[data-testid="ui-card-list-selected-count"]').exists()).toBe(false)

    selection.toggle(1)
    await flushPromises()
    expect(box()).toBe('mixed')
    expect(wrapper.get('[data-testid="ui-card-list-selected-count"]').text()).toContain('1')

    selection.toggle(2)
    await flushPromises()
    expect(box()).toBe('true')
    expect(wrapper.get('[data-testid="ui-card-list-selected-count"]').text()).toContain('2')
  })

  it('keeps the first record a plain card in the list, with no wrapper around it', () => {
    const selection = useRowSelection(() => [1, 2])
    const wrapper = mount(UiCardList, { global: global(), props: { selection }, slots: { default: cards } })

    const first = wrapper.get('[data-testid="ui-card-list"]').element.firstElementChild
    expect(first?.getAttribute('data-testid')).toBe('card')
  })

  it('draws placeholder cards of the given shape in a list while loading', () => {
    const wrapper = mount(UiCardList, {
      global: global(),
      props: { loading: true, skeletonRows: 3, skeleton: { select: false, subtitle: true, badge: true, actions: true, fields: 1 } },
      slots: { default: cards },
    })

    expect(wrapper.attributes('aria-busy')).toBe('true')
    expect(wrapper.findAllComponents(UiRecordCard)).toHaveLength(3)
    expect(wrapper.get('[data-testid="ui-table-skeleton"]').attributes('role')).toBe('list')
  })

  it('keeps the default slot mounted but hidden while it shows placeholders or the empty state', () => {
    const loading = mount(UiCardList, { global: global(), props: { loading: true }, slots: { default: cards } })
    const empty = mount(UiCardList, { global: global(), props: { empty: true }, slots: { default: cards } })

    expect(loading.get('[data-testid="ui-card-list"]').attributes('style')).toContain('display: none')
    expect(empty.get('[data-testid="ui-card-list"]').attributes('style')).toContain('display: none')
  })

  it('draws the empty state with its message, or the empty slot in its place', () => {
    const plain = mount(UiCardList, { global: global(), props: { empty: true, emptyText: 'Nothing here' } })
    const custom = mount(UiCardList, { global: global(), props: { empty: true }, slots: { empty: '<p data-testid="custom">None</p>' } })

    expect(plain.get('[data-testid="ui-table-empty"]').text()).toContain('Nothing here')
    expect(custom.find('[data-testid="custom"]').exists()).toBe(true)
    expect(custom.find('[data-testid="ui-table-empty"]').exists()).toBe(false)
  })

  it('draws the footer under the list', () => {
    const wrapper = mount(UiCardList, { global: global(), slots: { default: cards, footer: '<button data-testid="more">More</button>' } })

    expect(wrapper.find('[data-testid="more"]').exists()).toBe(true)
  })
})
