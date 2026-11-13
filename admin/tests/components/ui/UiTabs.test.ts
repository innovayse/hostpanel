/**
 * Unit tests for UiTabs: tablist semantics, selection and arrow-key navigation.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiTabs from '@/components/ui/UiTabs.vue'

/** Three tabs, the middle one disabled. */
const items = [
  { value: 'a', label: 'Alpha', testid: 'tab-a' },
  { value: 'b', label: 'Beta', disabled: true, testid: 'tab-b' },
  { value: 'c', label: 'Gamma', testid: 'tab-c' },
]

describe('UiTabs scrollbar', () => {
  it('keeps its scrollbar unless asked to hide it, so it cannot run into the selected tab\'s underline', () => {
    const items = [{ value: 'a', label: 'A' }, { value: 'b', label: 'B' }]
    const shown = mount(UiTabs, { props: { items, modelValue: 'a' } })
    const hidden = mount(UiTabs, { props: { items, modelValue: 'a', hideScrollbar: true } })

    expect(shown.get('[role="tablist"]').classes().join(' ')).not.toContain('scrollbar-width')
    expect(hidden.get('[role="tablist"]').classes().join(' ')).toContain('scrollbar-width:none')
  })
})

describe('UiTabs', () => {
  it('renders a tablist with the selected tab marked', () => {
    const wrapper = mount(UiTabs, { props: { items, modelValue: 'a' } })
    const tabs = wrapper.findAll('[role="tab"]')

    expect(wrapper.find('[role="tablist"]').exists()).toBe(true)
    expect(tabs.map(t => t.text())).toEqual(['Alpha', 'Beta', 'Gamma'])
    expect(tabs[0]?.attributes('aria-selected')).toBe('true')
    expect(tabs[2]?.attributes('aria-selected')).toBe('false')
  })

  it('hides vertical overflow on the horizontally scrollable tablist', () => {
    const wrapper = mount(UiTabs, { props: { items, modelValue: 'a' } })

    expect(wrapper.get('[role="tablist"]').classes()).toContain('overflow-y-hidden')
  })

  it('selects a tab on click', async () => {
    const wrapper = mount(UiTabs, { props: { items, modelValue: 'a' } })

    await wrapper.get('[data-testid="tab-c"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['c'])
  })

  it('does not select a disabled tab', async () => {
    const wrapper = mount(UiTabs, { props: { items, modelValue: 'a' } })

    await wrapper.get('[data-testid="tab-b"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
  })

  it('moves to the next enabled tab with the arrow keys, wrapping around', async () => {
    const wrapper = mount(UiTabs, { props: { items, modelValue: 'a' } })

    await wrapper.get('[data-testid="tab-a"]').trigger('keydown', { key: 'ArrowRight' })
    await wrapper.get('[data-testid="tab-a"]').trigger('keydown', { key: 'ArrowLeft' })

    expect(wrapper.emitted('update:modelValue')).toEqual([['c'], ['c']])
  })

  it('moves focus to the tab the arrow key selects and keeps only the selected tab in the tab order', async () => {
    const wrapper = mount(UiTabs, { props: { items, modelValue: 'a' }, attachTo: document.body })

    await wrapper.get('[data-testid="tab-a"]').trigger('keydown', { key: 'ArrowRight' })
    expect(document.activeElement).toBe(wrapper.get('[data-testid="tab-c"]').element)

    await wrapper.setProps({ modelValue: 'c' })
    expect(wrapper.get('[data-testid="tab-c"]').attributes('tabindex')).toBe('0')
    expect(wrapper.get('[data-testid="tab-a"]').attributes('tabindex')).toBe('-1')
    wrapper.unmount()
  })

  it('gives the scoped slot the active value', () => {
    const wrapper = mount(UiTabs, {
      props: { items, modelValue: 'c' },
      slots: { default: '<template #default="{ active }"><p data-testid="panel">{{ active }}</p></template>' },
    })

    expect(wrapper.get('[data-testid="panel"]').text()).toBe('c')
  })

  it('colours the selected tab with the primary accent and keeps idle tabs readable', () => {
    const wrapper = mount(UiTabs, { props: { items, modelValue: 'a' } })

    expect(wrapper.get('[data-testid="tab-a"]').classes()).toContain('text-primary-500')
    expect(wrapper.get('[data-testid="tab-c"]').classes()).toContain('text-text-secondary')
  })

  it('draws the slot in a panel set 22px under the tab line, and no panel without a slot', () => {
    const withSlot = mount(UiTabs, { props: { items, modelValue: 'a' }, slots: { default: '<p data-testid="body" />' } })
    const without = mount(UiTabs, { props: { items, modelValue: 'a' } })

    expect(withSlot.get('[data-testid="ui-tabs-panel"]').classes()).toContain('mt-5.5')
    expect(withSlot.find('[data-testid="ui-tabs-panel"] [data-testid="body"]').exists()).toBe(true)
    expect(without.find('[data-testid="ui-tabs-panel"]').exists()).toBe(false)
  })
})
