/**
 * Tests for UiChartHeatStrip: one square per value, shaded by its share of the largest value, a flat square for zero and
 * the tooltip the caller writes.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiChartHeatStrip from '@/components/ui/UiChartHeatStrip.vue'

/**
 * Writes the tooltip of a square.
 *
 * @param index - Zero based index of the square.
 * @param value - Its value.
 * @returns The text.
 */
const describeDay = (index: number, value: number): string => `Day ${index + 1}: ${value}`

describe('UiChartHeatStrip', () => {
  it('draws one square per value with the caller tooltip', () => {
    const wrapper = mount(UiChartHeatStrip, { props: { values: [0, 2, 4], describe: describeDay } })

    expect(wrapper.findAll('[data-testid="ui-chart-heat-cell"]')).toHaveLength(3)
    expect(wrapper.text()).toContain('Day 1: 0')
    expect(wrapper.text()).toContain('Day 3: 4')
  })

  it('keeps zero flat and gives the peak the strongest shade', () => {
    const cells = mount(UiChartHeatStrip, { props: { values: [0, 1, 4], describe: describeDay } }).findAll('[data-testid="ui-chart-heat-cell"]')

    expect(cells[0]?.classes()).toContain('bg-surface-track')
    expect(cells[1]?.classes()).toContain('opacity-50')
    expect(cells[2]?.classes()).toContain('opacity-100')
  })

  it('draws an all zero strip flat', () => {
    const cells = mount(UiChartHeatStrip, { props: { values: [0, 0], describe: describeDay } }).findAll('[data-testid="ui-chart-heat-cell"]')

    expect(cells.every(cell => cell.classes().includes('bg-surface-track'))).toBe(true)
  })
})

describe('UiChartHeatStrip on a phone', () => {
  it('scrolls sideways inside its box and keeps the size of every square', () => {
    const wrapper = mount(UiChartHeatStrip, { props: { values: [1, 2], describe: describeDay } })

    expect(wrapper.get('[data-testid="ui-chart-heat-strip"]').classes()).toEqual(expect.arrayContaining(['max-w-full', 'max-sm:overflow-x-auto']))
    expect(wrapper.get('[data-testid="ui-chart-heat-strip"]').element.children[0]?.classList.contains('shrink-0')).toBe(true)
  })
})

describe('UiChartHeatStrip tooltips', () => {
  it('has a tooltip role per square', () => {
    expect(mount(UiChartHeatStrip, { props: { values: [1], describe: describeDay } }).find('[role="tooltip"]').exists()).toBe(true)
  })
})
