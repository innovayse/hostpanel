/**
 * Tests for UiChartGauge: the arc fills the share of the scale, the value is clamped and the figure is written.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiChartGauge from '@/components/ui/UiChartGauge.vue'

/**
 * Mounts the gauge.
 *
 * @param value - The measured value.
 * @param max - The full scale.
 * @returns The wrapper.
 */
const mountGauge = (value: number, max = 100) => mount(UiChartGauge, { props: { value, max, label: 'Load', text: `${value}%` } })

describe('UiChartGauge', () => {
  it('fills the arc by the share of the scale', () => {
    expect(mountGauge(25).get('[data-testid="ui-chart-gauge-arc"]').attributes('stroke-dasharray')).toBe('25 100')
    expect(mountGauge(40, 80).get('[data-testid="ui-chart-gauge-arc"]').attributes('stroke-dasharray')).toBe('50 100')
  })

  it('clamps a value past the scale or below zero', () => {
    expect(mountGauge(250).get('[data-testid="ui-chart-gauge-arc"]').attributes('stroke-dasharray')).toBe('100 100')
    expect(mountGauge(-5).get('[data-testid="ui-chart-gauge-arc"]').attributes('stroke-dasharray')).toBe('0 100')
  })

  it('draws an empty arc for a scale that is not positive', () => {
    expect(mountGauge(5, 0).get('[data-testid="ui-chart-gauge-arc"]').attributes('stroke-dasharray')).toBe('0 100')
  })

  it('writes the figure and the caption and reads as a meter', () => {
    const wrapper = mountGauge(40)

    expect(wrapper.get('[data-testid="ui-chart-gauge-text"]').text()).toBe('40%')
    expect(wrapper.text()).toContain('Load')
    expect(wrapper.attributes('role')).toBe('meter')
    expect(wrapper.attributes('aria-valuetext')).toBe('40%')
  })
})
