/**
 * Unit tests for UiProgress: the label and the rounded percentage, the native bar's value, and a value
 * clamped to the bar.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiProgress from '@/components/ui/UiProgress.vue'

describe('UiProgress', () => {
  it('shows the label and the rounded percentage on a native bar', () => {
    const wrapper = mount(UiProgress, { props: { value: 42.6, label: 'Storage' } })

    expect(wrapper.text()).toContain('Storage')
    expect(wrapper.get('[data-testid="ui-progress-value"]').text()).toBe('43%')
    expect(wrapper.get('progress').attributes()).toEqual(expect.objectContaining({ value: '42.6', max: '100', 'aria-label': 'Storage' }))
  })

  it('shows the value caption of the caller instead of the percentage while the bar still draws the value', () => {
    const wrapper = mount(UiProgress, { props: { value: 12, label: 'Accounts', valueText: '12 / 100' } })

    expect(wrapper.get('[data-testid="ui-progress-value"]').text()).toBe('12 / 100')
    expect(wrapper.get('progress').attributes('value')).toBe('12')
  })

  it('fills the bar with the blue to violet gradient below the warning threshold', () => {
    const bar = mount(UiProgress, { props: { value: 60, label: 'Storage' } }).get('progress')

    expect(bar.attributes('data-state')).toBe('normal')
    expect(bar.classes().join(' ')).toContain('var(--gradient-bar)')
  })

  it('turns the bar to the warning colour at the threshold', () => {
    const bar = mount(UiProgress, { props: { value: 85, label: 'Storage' } }).get('progress')

    expect(bar.attributes('data-state')).toBe('warning')
    expect(bar.classes()).toContain('[&::-webkit-progress-value]:bg-[image:var(--gradient-bar-warn)]')
  })

  it('draws placeholder bars and no native bar while loading', () => {
    const wrapper = mount(UiProgress, { props: { value: 40, label: 'Storage', loading: true } })

    expect(wrapper.find('progress').exists()).toBe(false)
    expect(wrapper.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(3)
    expect(wrapper.text()).not.toContain('Storage')
    expect(wrapper.attributes('aria-busy')).toBe('true')
  })

  it('takes the threshold from warnAt', () => {
    expect(mount(UiProgress, { props: { value: 50, label: 'A', warnAt: 40 } }).get('progress').attributes('data-state')).toBe('warning')
    expect(mount(UiProgress, { props: { value: 95, label: 'A', warnAt: 100 } }).get('progress').attributes('data-state')).toBe('normal')
  })

  it('clamps a value outside the bar and reads a non-number as zero', () => {
    expect(mount(UiProgress, { props: { value: 140, label: 'A' } }).get('[data-testid="ui-progress-value"]').text()).toBe('100%')
    expect(mount(UiProgress, { props: { value: -5, label: 'B' } }).get('[data-testid="ui-progress-value"]').text()).toBe('0%')
    expect(mount(UiProgress, { props: { value: Number.NaN, label: 'C' } }).get('[data-testid="ui-progress-value"]').text()).toBe('0%')
  })
})
