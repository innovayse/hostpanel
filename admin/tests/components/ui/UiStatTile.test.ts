/**
 * Unit tests for UiStatTile: caption, value, hint, tone and slots.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiStatTile from '@/components/ui/UiStatTile.vue'

describe('UiStatTile', () => {
  it('shows the caption and the value', () => {
    const wrapper = mount(UiStatTile, { props: { label: 'Revenue', value: '$1,200' } })

    expect(wrapper.text()).toContain('Revenue')
    expect(wrapper.text()).toContain('$1,200')
  })

  it('shows the hint only when given', () => {
    expect(mount(UiStatTile, { props: { label: 'a', value: 1 } }).text()).not.toContain('since')
    expect(mount(UiStatTile, { props: { label: 'a', value: 1, hint: 'since May' } }).text()).toContain('since May')
  })

  it('colours the value from the tone', () => {
    const wrapper = mount(UiStatTile, { props: { label: 'Overdue', value: 3, tone: 'danger' } })

    expect(wrapper.get('p.font-display').classes()).toContain('text-status-red')
  })

  it('sets the value in tabular figures so digits line up across tiles', () => {
    expect(mount(UiStatTile, { props: { label: 'Revenue', value: 1200 } }).get('p.font-display').classes()).toContain('tabular-nums')
  })

  it('renders the icon and trend slots', () => {
    const wrapper = mount(UiStatTile, {
      props: { label: 'a', value: 1 },
      slots: { icon: '<i data-testid="icon" />', trend: '<b data-testid="trend" />' },
    })

    expect(wrapper.find('[data-testid="icon"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="trend"]').exists()).toBe(true)
  })
})
