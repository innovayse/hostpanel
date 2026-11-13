/**
 * Unit tests for UiSectionHeader: title left, actions right, wrapping.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiSectionHeader from '@/components/ui/UiSectionHeader.vue'

describe('UiSectionHeader', () => {
  it('renders the title as a heading of the chosen level', () => {
    const wrapper = mount(UiSectionHeader, { props: { level: 3 }, slots: { default: 'Title' } })

    expect(wrapper.get('h3').text()).toBe('Title')
  })

  it('lays the title and the actions on a wrapping space-between row', () => {
    const wrapper = mount(UiSectionHeader, { slots: { default: 'Title', actions: '<button>Go</button>' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['flex-row', 'justify-between', 'flex-wrap']))
    expect(wrapper.get('[data-testid="ui-section-header-actions"]').text()).toBe('Go')
  })

  it('draws no actions wrapper without the slot', () => {
    const wrapper = mount(UiSectionHeader, { slots: { default: 'Title' } })

    expect(wrapper.find('[data-testid="ui-section-header-actions"]').exists()).toBe(false)
  })
})
