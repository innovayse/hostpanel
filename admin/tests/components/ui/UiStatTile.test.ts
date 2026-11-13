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

  it('shows the change in its tone, with its sign in the text', () => {
    const wrapper = mount(UiStatTile, { props: { label: 'Revenue', value: 10, delta: '+12%', deltaTone: 'success' } })

    expect(wrapper.get('[data-testid="ui-stat-tile-delta"]').text()).toBe('+12%')
    expect(wrapper.get('[data-testid="ui-stat-tile-delta"]').classes()).toContain('text-status-green')
  })

  it('draws no change unless one is given', () => {
    expect(mount(UiStatTile, { props: { label: 'a', value: 1 } }).find('[data-testid="ui-stat-tile-delta"]').exists()).toBe(false)
  })

  it('renders the icon and trend slots', () => {
    const wrapper = mount(UiStatTile, {
      props: { label: 'a', value: 1 },
      slots: { icon: '<i data-testid="icon" />', trend: '<b data-testid="trend" />' },
    })

    expect(wrapper.find('[data-testid="icon"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="trend"]').exists()).toBe(true)
  })

  it('draws the value slot in place of the plain value', () => {
    const wrapper = mount(UiStatTile, { props: { label: 'a', value: 1 }, slots: { value: '<b data-testid="custom">Live</b>' } })

    expect(wrapper.get('p.font-display [data-testid="custom"]').text()).toBe('Live')
    expect(wrapper.get('p.font-display').text()).toBe('Live')
  })

  it('sizes the caption, value and change as the concept tile', () => {
    const wrapper = mount(UiStatTile, { props: { label: 'Revenue', value: 1, delta: '+1%' } })

    expect(wrapper.get('p.font-display').classes()).toEqual(expect.arrayContaining(['text-[1.875rem]', 'font-semibold']))
    expect(wrapper.get('[data-testid="ui-stat-tile-delta"]').classes()).toContain('text-xs')
    expect(wrapper.classes()).toContain('gap-2.5')
  })

  it('keeps the caption and hint and draws bars for the value and change while loading', () => {
    const wrapper = mount(UiStatTile, {
      props: { label: 'Revenue', value: '$1,200', delta: '+12%', hint: 'since May', loading: true },
      slots: { icon: '<i data-testid="icon" />', trend: '<b data-testid="trend" />' },
    })

    expect(wrapper.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(2)
    expect(wrapper.text()).toContain('Revenue')
    expect(wrapper.text()).toContain('since May')
    expect(wrapper.find('[data-testid="icon"]').exists()).toBe(true)
    expect(wrapper.text()).not.toContain('$1,200')
    expect(wrapper.text()).not.toContain('+12%')
    expect(wrapper.find('[data-testid="trend"]').exists()).toBe(false)
    expect(wrapper.attributes('aria-busy')).toBe('true')
  })
})
