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
})
