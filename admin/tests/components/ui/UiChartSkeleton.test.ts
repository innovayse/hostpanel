/**
 * Unit tests for UiChartSkeleton: the placeholder of a chart in the shape of its kind, in the chart's box or in a box of
 * a plot height.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiChartSkeleton from '@/components/ui/UiChartSkeleton.vue'
import { ChartKind, chartSkeletonBars, chartSkeletonRows, chartSkeletonSquares, UiChartHeight } from '@/utils/ui/chart'

describe('UiChartSkeleton', () => {
  it('draws one bar per placeholder height, hidden from assistive technology', () => {
    const wrapper = mount(UiChartSkeleton)

    expect(wrapper.attributes('aria-hidden')).toBe('true')
    expect(wrapper.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(chartSkeletonBars.length)
  })

  it('fills the box it is put in when no height is given', () => {
    expect(mount(UiChartSkeleton).classes()).toContain('h-full')
  })

  it('takes the plot height of the chart when one is given, and its phone height below sm', () => {
    const wrapper = mount(UiChartSkeleton, { props: { height: UiChartHeight.Lg } })

    expect(wrapper.classes()).toContain('h-80')
    expect(wrapper.classes()).toContain('max-sm:h-64')
    expect(wrapper.classes()).not.toContain('h-full')
  })

  it('takes the phone height of its kind, the one the chart takes', () => {
    expect(mount(UiChartSkeleton, { props: { kind: ChartKind.Donut, height: UiChartHeight.Md } }).classes()).toContain('max-sm:h-72')
    expect(mount(UiChartSkeleton, { props: { kind: ChartKind.HeatStrip, height: UiChartHeight.Md } }).classes()).toContain('max-sm:h-auto')
  })

  it.each([ChartKind.GroupedBar, ChartKind.StackedBar])('draws vertical bars for %s', (kind) => {
    const wrapper = mount(UiChartSkeleton, { props: { kind } })

    expect(wrapper.attributes('data-kind')).toBe(kind)
    expect(wrapper.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(chartSkeletonBars.length)
  })

  it('draws bars of different lengths for the horizontal bar kind', () => {
    const wrapper = mount(UiChartSkeleton, { props: { kind: ChartKind.HorizontalBar } })

    expect(wrapper.attributes('data-kind')).toBe(ChartKind.HorizontalBar)
    expect(wrapper.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(chartSkeletonRows.length)
  })

  it('draws a line for the line kind and fills under it for the area kind', () => {
    const line = mount(UiChartSkeleton, { props: { kind: ChartKind.Line } })
    const area = mount(UiChartSkeleton, { props: { kind: ChartKind.Area } })

    expect(line.find('svg path').exists()).toBe(true)
    expect(line.find('[data-testid="chart-skeleton-area"]').exists()).toBe(false)
    expect(area.find('[data-testid="chart-skeleton-area"]').exists()).toBe(true)
  })

  it('draws a ring for the donut kind', () => {
    const wrapper = mount(UiChartSkeleton, { props: { kind: ChartKind.Donut } })

    expect(wrapper.attributes('data-kind')).toBe(ChartKind.Donut)
    expect(wrapper.find('svg circle').exists()).toBe(true)
  })

  it('draws the legend bars of a donut under the ring on a phone only', () => {
    const legend = mount(UiChartSkeleton, { props: { kind: ChartKind.Donut } }).get('[data-testid="chart-skeleton-legend"]')

    expect(legend.classes()).toContain('sm:hidden')
    expect(legend.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(3)
  })

  it('draws a row of squares for the heat strip kind', () => {
    const wrapper = mount(UiChartSkeleton, { props: { kind: ChartKind.HeatStrip } })

    expect(wrapper.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(chartSkeletonSquares)
  })

  it('draws one wide bar over its key for the distribution kind', () => {
    const wrapper = mount(UiChartSkeleton, { props: { kind: ChartKind.Distribution } })

    expect(wrapper.attributes('data-kind')).toBe(ChartKind.Distribution)
    expect(wrapper.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(4)
    expect(wrapper.get('.flex-wrap').findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(3)
  })

  it('draws a half ring for the gauge kind', () => {
    const wrapper = mount(UiChartSkeleton, { props: { kind: ChartKind.Gauge } })

    expect(wrapper.attributes('data-kind')).toBe(ChartKind.Gauge)
    expect(wrapper.find('svg path').exists()).toBe(true)
  })
})
