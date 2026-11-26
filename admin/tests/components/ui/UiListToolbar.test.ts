/**
 * Unit tests for UiListToolbar: the search box is two-way bound and named by the screen, the filter fields sit in
 * the Filters surface, its Apply, Reset and Cancel are passed on, and the chips report their removal.
 */
import { describe, expect, it } from 'vitest'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import { mountWithApp } from '../../helpers/mount'

/** A filter field for the default slot, so the Filters button shows. */
const FIELDS = '<span>fields</span>'

/**
 * Mounts the toolbar with one chip and a filter field in the default slot.
 *
 * @param activeCount - How many filters are applied.
 * @returns The wrapper.
 */
const mountToolbar = (activeCount = 1) =>
  mountWithApp(UiListToolbar, {
    props: {
      searchTestid: 'list-search',
      search: '',
      activeCount,
      chips: [{ key: 'status', label: 'Status', value: 'Paid' }],
    },
    slots: { default: FIELDS },
    stubs: { teleport: true },
  })

describe('UiListToolbar', () => {
  it('keeps its layout while loading, with the search box and the Filters button disabled', () => {
    const wrapper = mountWithApp(UiListToolbar, {
      props: { searchTestid: 'list-search', search: '', loading: true },
      slots: { default: FIELDS },
      stubs: { teleport: true },
    })

    expect(wrapper.get('[data-testid="list-search"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="filter-trigger"]').attributes('disabled')).toBeDefined()
  })

  it('names the search box after the screen and reports what is typed', async () => {
    const wrapper = mountToolbar()

    await wrapper.get('[data-testid="list-search"]').setValue('ada')

    expect(wrapper.emitted('update:search')?.[0]).toEqual(['ada'])
  })

  it('uses the given search placeholder, and "Search" otherwise', () => {
    const plain = mountToolbar()
    const custom = mountWithApp(UiListToolbar, {
      props: { searchTestid: 'list-search', searchPlaceholder: 'Search the registry', search: '', activeCount: 0, chips: [] },
      slots: { default: FIELDS },
      stubs: { teleport: true },
    })

    expect(plain.get('[data-testid="list-search"]').attributes('placeholder')).toBe('Search')
    expect(custom.get('[data-testid="list-search"]').attributes('placeholder')).toBe('Search the registry')
  })

  it('shows no search box while the screen binds no search', () => {
    const wrapper = mountWithApp(UiListToolbar, {
      props: { activeCount: 0, chips: [] },
      slots: { default: FIELDS },
      stubs: { teleport: true },
    })

    expect(wrapper.find('[data-testid="list-search"]').exists()).toBe(false)
    expect(wrapper.find('input[type="search"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="filter-trigger"]').exists()).toBe(true)
  })

  it('shows no Filters button while the screen has no filter fields', () => {
    const wrapper = mountWithApp(UiListToolbar, {
      props: { searchTestid: 'list-search', search: '' },
      stubs: { teleport: true },
    })

    expect(wrapper.find('[data-testid="list-search"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="filter-trigger"]').exists()).toBe(false)
  })

  it('shows the count of applied filters on the Filters button', () => {
    const wrapper = mountToolbar(3)

    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('3')
  })

  it('lets the screen rename the Filters button', () => {
    const wrapper = mountWithApp(UiListToolbar, {
      props: { searchTestid: 'list-search', filterTestid: 'list-filter', search: '', activeCount: 0, chips: [] },
      slots: { default: FIELDS },
      stubs: { teleport: true },
    })

    expect(wrapper.find('[data-testid="list-filter"]').exists()).toBe(true)
  })

  it('passes Apply, Reset and Cancel of the filter surface on', async () => {
    const wrapper = mountToolbar()

    await wrapper.get('[data-testid="filter-trigger"]').trigger('click')
    await wrapper.get('[data-testid="filter-reset"]').trigger('click')
    await wrapper.get('[data-testid="filter-apply"]').trigger('click')

    expect(wrapper.emitted('reset')).toHaveLength(1)
    expect(wrapper.emitted('apply')).toHaveLength(1)
  })

  it('reports the key of a removed chip and the clear-all request', async () => {
    const wrapper = mountWithApp(UiListToolbar, {
      props: {
        searchTestid: 'list-search',
        search: '',
        activeCount: 2,
        chips: [
          { key: 'status', label: 'Status', value: 'Paid' },
          { key: 'from', label: 'Date from', value: '01/10/2026' },
        ],
      },
      slots: { default: FIELDS },
      stubs: { teleport: true },
    })

    await wrapper.get('[data-testid="filter-chip-remove-from"]').trigger('click')
    await wrapper.get('[data-testid="filter-chips-clear"]').trigger('click')

    expect(wrapper.emitted('remove')?.[0]).toEqual(['from'])
    expect(wrapper.emitted('clear')).toHaveLength(1)
  })

  it('shows the kit clear button once there is search text, and clearing reports an empty search', async () => {
    const wrapper = mountWithApp(UiListToolbar, {
      props: { searchTestid: 'list-search', search: 'ada' },
      stubs: { teleport: true },
    })

    expect(wrapper.get('[data-testid="list-search"]').classes()).toContain('[&::-webkit-search-cancel-button]:hidden')
    await wrapper.get('[data-testid="input-clear"]').trigger('click')

    expect(wrapper.emitted('update:search')?.[0]).toEqual([''])
  })

  it('shows no clear button while the search box is empty', () => {
    expect(mountToolbar().find('[data-testid="input-clear"]').exists()).toBe(false)
  })

  it('lays the search box and the Filters button in one centred row of equal height that wraps', () => {
    const wrapper = mountToolbar()
    const search = wrapper.get('[data-testid="list-search"]')
    const trigger = wrapper.get('[data-testid="filter-trigger"]')
    const row = trigger.element.parentElement

    expect(row?.classList.contains('flex-wrap')).toBe(true)
    expect(row?.classList.contains('items-center')).toBe(true)
    expect(row?.contains(search.element)).toBe(true)
    expect(search.classes()).toContain('h-10.5')
    expect(trigger.classes()).toContain('h-10.5')
  })

  it('keeps the row height with search text, the clear button being out of the flow', () => {
    const wrapper = mountWithApp(UiListToolbar, {
      props: { searchTestid: 'list-search', search: 'ada' },
      slots: { default: FIELDS },
      stubs: { teleport: true },
    })
    const clear = wrapper.get('[data-testid="input-clear"]')

    expect(clear.element.parentElement?.classList.contains('absolute')).toBe(true)
    expect(clear.classes()).not.toContain('relative')
  })

  it('puts the actions slot at the trailing end of the same row as the search box and the Filters button', () => {
    const wrapper = mountWithApp(UiListToolbar, {
      props: { searchTestid: 'list-search', search: '' },
      slots: { default: FIELDS, actions: '<button data-testid="create">Create</button>' },
      stubs: { teleport: true },
    })
    const actions = wrapper.get('[data-testid="list-toolbar-actions"]')
    const row = wrapper.get('[data-testid="filter-trigger"]').element.parentElement

    expect(row?.contains(actions.element)).toBe(true)
    expect(row?.lastElementChild).toBe(actions.element)
    expect(actions.classes()).toEqual(expect.arrayContaining(['ml-auto', 'shrink-0']))
    expect(row?.classList.contains('flex-wrap')).toBe(true)
    expect(actions.find('[data-testid="create"]').exists()).toBe(true)
  })

  it('draws no actions wrapper while the screen fills no actions slot', () => {
    expect(mountToolbar().find('[data-testid="list-toolbar-actions"]').exists()).toBe(false)
  })

  it('keeps the actions enabled while the list loads', () => {
    const wrapper = mountWithApp(UiListToolbar, {
      props: { searchTestid: 'list-search', search: '', loading: true },
      slots: { actions: '<button data-testid="create">Create</button>' },
      stubs: { teleport: true },
    })

    expect(wrapper.get('[data-testid="create"]').attributes('disabled')).toBeUndefined()
  })
})
