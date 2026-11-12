/**
 * Unit tests for UiTabs: the tab list semantics, selection, the badge and the panel slot.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiTabs from '~/components/ui/UiTabs.vue'

/** Tabs used by every test. */
const TABS = [
  { value: 'overview', label: 'Overview' },
  { value: 'billing', label: 'Billing', badge: 3 },
]

describe('UiTabs', () => {
  it('renders a labelled tab list with one tab per entry', () => {
    const wrapper = mountWithKit(UiTabs, { props: { tabs: TABS, modelValue: 'overview' } })

    expect(wrapper.find('[role="tablist"]').attributes('aria-label')).toBe('ui.tabs')
    expect(wrapper.findAll('[role="tab"]')).toHaveLength(2)
  })

  it('uses the aria label it is given', () => {
    const wrapper = mountWithKit(UiTabs, { props: { tabs: TABS, modelValue: 'overview', ariaLabel: 'Account sections' } })

    expect(wrapper.find('[role="tablist"]').attributes('aria-label')).toBe('Account sections')
  })

  it('marks the active tab selected', () => {
    const wrapper = mountWithKit(UiTabs, { props: { tabs: TABS, modelValue: 'billing' } })

    expect(wrapper.find('[data-testid="ui-tab-billing"]').attributes('aria-selected')).toBe('true')
    expect(wrapper.find('[data-testid="ui-tab-overview"]').attributes('aria-selected')).toBe('false')
    expect(wrapper.find('[data-testid="ui-tab-billing"]').classes()).toContain('border-primary-500')
  })

  it('emits the value of the clicked tab', async () => {
    const wrapper = mountWithKit(UiTabs, { props: { tabs: TABS, modelValue: 'overview' } })

    await wrapper.find('[data-testid="ui-tab-billing"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['billing'])
  })

  it('shows the badge of a tab', () => {
    const wrapper = mountWithKit(UiTabs, { props: { tabs: TABS, modelValue: 'overview' } })

    expect(wrapper.find('[data-testid="ui-tab-billing"]').text()).toContain('3')
  })

  it('renders the panel slot only when given', () => {
    const without = mountWithKit(UiTabs, { props: { tabs: TABS, modelValue: 'overview' } })
    const withPanel = mountWithKit(UiTabs, { props: { tabs: TABS, modelValue: 'overview' }, slots: { default: 'Panel body' } })

    expect(without.text()).not.toContain('Panel body')
    expect(withPanel.text()).toContain('Panel body')
  })

  it('scrolls the bar sideways and keeps every tab at its natural width when the tabs do not fit', () => {
    const wrapper = mountWithKit(UiTabs, { props: { tabs: TABS, modelValue: 'overview' } })

    expect(wrapper.find('[role="tablist"]').classes()).toContain('overflow-x-auto')
    expect(wrapper.find('[data-testid="ui-tab-billing"]').classes()).toContain('shrink-0')
  })
})
