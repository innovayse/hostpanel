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

  it('clamps a value outside the bar and reads a non-number as zero', () => {
    expect(mount(UiProgress, { props: { value: 140, label: 'A' } }).get('[data-testid="ui-progress-value"]').text()).toBe('100%')
    expect(mount(UiProgress, { props: { value: -5, label: 'B' } }).get('[data-testid="ui-progress-value"]').text()).toBe('0%')
    expect(mount(UiProgress, { props: { value: Number.NaN, label: 'C' } }).get('[data-testid="ui-progress-value"]').text()).toBe('0%')
  })
})
