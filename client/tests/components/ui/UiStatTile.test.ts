/**
 * Unit tests for UiStatTile: the label and value, the icon tone, and the hint with its trend.
 */
import { describe, expect, it } from 'vitest'
import { Users } from 'lucide-vue-next'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiStatTile from '~/components/ui/UiStatTile.vue'
import { UiStatTrend, UiTone } from '~/utils/ui/enums'

describe('UiStatTile', () => {
  it('shows the label and the value inside a card', () => {
    const wrapper = mountWithKit(UiStatTile, { props: { label: 'Active services', value: 12 } })

    expect(wrapper.text()).toContain('Active services')
    expect(wrapper.find('[data-testid="ui-stat-tile-value"]').text()).toBe('12')
    expect(wrapper.classes()).toContain('rounded-2xl')
  })

  it('shows no icon badge or hint without them', () => {
    const wrapper = mountWithKit(UiStatTile, { props: { label: 'x', value: 1 } })

    expect(wrapper.find('svg').exists()).toBe(false)
    expect(wrapper.find('.text-xs.flex').exists()).toBe(false)
  })

  it('draws the icon in the tone colour', () => {
    const wrapper = mountWithKit(UiStatTile, { props: { label: 'x', value: 1, icon: Users, tone: UiTone.Success } })

    expect(wrapper.find('svg').exists()).toBe(true)
    expect(wrapper.html()).toContain('bg-green-500/10')
  })

  it.each([
    [UiStatTrend.Up, 'text-green-600'],
    [UiStatTrend.Down, 'text-red-600'],
    [UiStatTrend.Flat, 'text-gray-500'],
  ])('colours the %s hint with %s', (trend, expected) => {
    const wrapper = mountWithKit(UiStatTile, { props: { label: 'x', value: 1, hint: '+4%', trend } })

    expect(wrapper.find('.text-xs.flex').classes()).toContain(expected)
    expect(wrapper.find('.text-xs.flex').text()).toBe('+4%')
  })

  it('adds an arrow for up and down but not for flat', () => {
    const up = mountWithKit(UiStatTile, { props: { label: 'x', value: 1, hint: '+4%', trend: UiStatTrend.Up } })
    const flat = mountWithKit(UiStatTile, { props: { label: 'x', value: 1, hint: '0%', trend: UiStatTrend.Flat } })

    expect(up.find('.text-xs.flex svg').exists()).toBe(true)
    expect(flat.find('.text-xs.flex svg').exists()).toBe(false)
  })

  it('draws the value slot in place of the plain value', () => {
    const wrapper = mountWithKit(UiStatTile, { props: { label: 'x', value: 1 }, slots: { value: '<b data-testid="custom">Live</b>' } })

    expect(wrapper.find('[data-testid="ui-stat-tile-value"] [data-testid="custom"]').text()).toBe('Live')
    expect(wrapper.find('[data-testid="ui-stat-tile-value"]').text()).not.toContain('1')
  })
})
