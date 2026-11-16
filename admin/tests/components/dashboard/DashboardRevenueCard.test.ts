/**
 * Tests for DashboardRevenueCard: the chart gets the days of the period and the daily average as its target, and
 * the title row shows the total with its change.
 */
import { describe, expect, it } from 'vitest'
import DashboardRevenueCard from '@/components/dashboard/DashboardRevenueCard.vue'
import UiChart from '@/components/ui/UiChart.vue'
import { dashboardCharts } from '../../helpers/dashboardFixtures'
import { mountWithApp } from '../../helpers/mount'

describe('DashboardRevenueCard', () => {
  it('hands one bar per day and the daily average as the target to the chart', () => {
    const wrapper = mountWithApp(DashboardRevenueCard, { props: { income: dashboardCharts().income, currency: 'USD', loading: false }, stubs: { UiChart: true } })
    const chart = wrapper.getComponent(UiChart)

    expect(chart.props('height')).toBe('short')
    expect(chart.props('variant')).toBe('minimal')
    expect(chart.props('labels')).toEqual(['Mar 10', 'Mar 11', 'Mar 12'])
    expect(chart.props('series')).toEqual([{ label: 'Income', data: [0, 300, 150] }])
    expect(chart.props('target')).toBe(150)
    expect(chart.props('loading')).toBe(false)
  })

  it('shows the total and the signed change in the title row', () => {
    const wrapper = mountWithApp(DashboardRevenueCard, { props: { income: dashboardCharts().income, currency: 'USD', loading: false }, stubs: { UiChart: true } })

    expect(wrapper.get('[data-testid="dashboard-revenue-total"]').text()).toContain('$450')
    expect(wrapper.get('[data-testid="dashboard-revenue-change"]').text()).toBe('+50%')
  })

  it('shows no change when the previous period earned nothing', () => {
    const income = { ...dashboardCharts().income, changePercent: null }
    const wrapper = mountWithApp(DashboardRevenueCard, { props: { income, currency: 'USD', loading: false }, stubs: { UiChart: true } })

    expect(wrapper.find('[data-testid="dashboard-revenue-change"]').exists()).toBe(false)
  })

  it('keeps the title row and loads the chart before the first answer', () => {
    const wrapper = mountWithApp(DashboardRevenueCard, { props: { income: null, currency: '', loading: true }, stubs: { UiChart: true } })

    expect(wrapper.text()).toContain('Revenue against target')
    expect(wrapper.getComponent(UiChart).props('loading')).toBe(true)
  })

  it('loads the chart while another period is on its way', () => {
    const wrapper = mountWithApp(DashboardRevenueCard, { props: { income: dashboardCharts().income, currency: 'USD', loading: true }, stubs: { UiChart: true } })

    expect(wrapper.getComponent(UiChart).props('loading')).toBe(true)
  })
})
