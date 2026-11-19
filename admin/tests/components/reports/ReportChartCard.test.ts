/**
 * Tests for ReportChartCard: the title and the data handed to UiChart, which draws it.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { computed, ref } from 'vue'
import ReportChartCard from '@/components/reports/ReportChartCard.vue'
import UiChart from '@/components/ui/UiChart.vue'
import { REPORT_EMPTY } from '@/lib/reportEmpty'
import { REPORT_LOADING } from '@/lib/reportLoading'
import { ChartKind, UiChartHeight } from '@/utils/ui/chart'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the card with UiChart replaced by a stub, as a canvas cannot draw in the test environment.
 *
 * @param props - Props of the card.
 * @returns The wrapper.
 */
const mountCard = (props: Record<string, unknown>) =>
  mount(ReportChartCard, {
    props: { chart: { kind: ChartKind.GroupedBar }, labels: ['Jan'], series: [{ label: 'Income', data: [3] }], ...props },
    global: { plugins: [createTestI18n()], stubs: { UiChart: true } },
  })

describe('ReportChartCard', () => {
  it('shows the title when given', () => {
    expect(mountCard({ title: 'Income' }).text()).toContain('Income')
  })

  it('keeps the title and hands the chart its loading state while the report loads', () => {
    const wrapper = mount(ReportChartCard, {
      props: { chart: { kind: ChartKind.GroupedBar }, labels: ['Jan'], series: [{ label: 'Income', data: [3] }], title: 'Income' },
      global: { plugins: [createTestI18n()], stubs: { UiChart: true }, provide: { [REPORT_LOADING as symbol]: ref(true) } },
    })

    expect(wrapper.text()).toContain('Income')
    expect(wrapper.attributes('aria-busy')).toBe('true')
    expect(wrapper.findComponent(UiChart).props('loading')).toBe(true)
  })

  it('lets the controls beside the title wrap under it on a phone', () => {
    const wrapper = mountCard({ title: 'Income' })

    expect(wrapper.get('h2').element.parentElement?.classList.contains('flex-wrap')).toBe(true)
  })

  it('has no heading without a title', () => {
    expect(mountCard({}).find('h2').exists()).toBe(false)
  })

  it('hands the chart its kind, labels, series and options', () => {
    const chart = mountCard({ chart: { kind: ChartKind.HorizontalBar, height: UiChartHeight.Lg }, title: 'Income' }).getComponent(UiChart)

    expect(chart.props('kind')).toBe(ChartKind.HorizontalBar)
    expect(chart.props('label')).toBe('Income')
    expect(chart.props('labels')).toEqual(['Jan'])
    expect(chart.props('series')).toEqual([{ label: 'Income', data: [3] }])
    expect(chart.props('height')).toBe('lg')
  })

  it('replaces the card with the compact empty state when there is nothing to plot', () => {
    const wrapper = mountCard({ labels: [], series: [] })

    expect(wrapper.find('[data-testid="report-chart"]').exists()).toBe(false)
    expect(wrapper.findComponent(UiChart).exists()).toBe(false)
    expect(wrapper.get('[data-testid="report-chart-empty"]').text()).toContain('No data for this report.')
  })

  it('treats a chart of only zero values as empty', () => {
    const wrapper = mountCard({ labels: ['Jan', 'Feb'], series: [{ label: 'Income', data: [0, 0] }] })

    expect(wrapper.find('[data-testid="report-chart-empty"]').exists()).toBe(true)
  })

  it('never shows the empty state while the report loads', () => {
    const wrapper = mount(ReportChartCard, {
      props: { chart: { kind: ChartKind.GroupedBar }, labels: [], series: [] },
      global: { plugins: [createTestI18n()], stubs: { UiChart: true }, provide: { [REPORT_LOADING as symbol]: ref(true) } },
    })

    expect(wrapper.find('[data-testid="report-chart-empty"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="report-chart"]').exists()).toBe(true)
  })

  it('draws nothing when the page shows its one empty state for the whole report', () => {
    const wrapper = mount(ReportChartCard, {
      props: { chart: { kind: ChartKind.GroupedBar }, labels: [], series: [] },
      global: {
        plugins: [createTestI18n()],
        stubs: { UiChart: true },
        provide: { [REPORT_EMPTY as symbol]: { set: () => undefined, remove: () => undefined, collapsed: computed(() => true) } },
      },
    })

    expect(wrapper.find('[data-testid="report-chart-empty"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="report-chart"]').exists()).toBe(false)
  })
})
