/**
 * Tests for ReportChartCard: the title and the data handed to UiChart, which draws it.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { ref } from 'vue'
import ReportChartCard from '@/components/reports/ReportChartCard.vue'
import UiChart from '@/components/ui/UiChart.vue'
import { REPORT_LOADING } from '@/lib/reportLoading'
import { UiChartType } from '@/utils/ui/chart'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the card with UiChart replaced by a stub, as a canvas cannot draw in the test environment.
 *
 * @param props - Props of the card.
 * @returns The wrapper.
 */
const mountCard = (props: Record<string, unknown>) =>
  mount(ReportChartCard, {
    props: { type: UiChartType.Bar, labels: ['Jan'], series: [{ label: 'Income', data: [3] }], ...props },
    global: { plugins: [createTestI18n()], stubs: { UiChart: true } },
  })

describe('ReportChartCard', () => {
  it('shows the title when given', () => {
    expect(mountCard({ title: 'Income' }).text()).toContain('Income')
  })

  it('keeps the title and draws a skeleton instead of the chart while the report loads', () => {
    const wrapper = mount(ReportChartCard, {
      props: { type: UiChartType.Bar, labels: ['Jan'], series: [{ label: 'Income', data: [3] }], title: 'Income' },
      global: { plugins: [createTestI18n()], stubs: { UiChart: true }, provide: { [REPORT_LOADING as symbol]: ref(true) } },
    })

    expect(wrapper.text()).toContain('Income')
    expect(wrapper.find('[data-testid="ui-card-skeleton"]').exists()).toBe(true)
    expect(wrapper.findComponent(UiChart).exists()).toBe(false)
  })

  it('has no heading without a title', () => {
    expect(mountCard({}).find('h2').exists()).toBe(false)
  })

  it('hands the chart its kind, labels, series and options', () => {
    const chart = mountCard({ horizontal: true, height: 'lg' }).getComponent(UiChart)

    expect(chart.props('type')).toBe('bar')
    expect(chart.props('labels')).toEqual(['Jan'])
    expect(chart.props('series')).toEqual([{ label: 'Income', data: [3] }])
    expect(chart.props('horizontal')).toBe(true)
    expect(chart.props('height')).toBe('lg')
  })
})
