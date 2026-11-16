/**
 * Tests for DashboardGrowthCard: two lines over the same days, the totals in the title row, zero days kept.
 */
import { describe, expect, it } from 'vitest'
import DashboardGrowthCard from '@/components/dashboard/DashboardGrowthCard.vue'
import UiChart from '@/components/ui/UiChart.vue'
import { dashboardCharts } from '../../helpers/dashboardFixtures'
import { mountWithApp } from '../../helpers/mount'

describe('DashboardGrowthCard', () => {
  it('draws a line of orders and a line of clients, zero days included', () => {
    const { newOrders, newClients } = dashboardCharts()
    const wrapper = mountWithApp(DashboardGrowthCard, { props: { newOrders, newClients, loading: false }, stubs: { UiChart: true } })
    const chart = wrapper.getComponent(UiChart)

    expect(chart.props('type')).toBe('line')
    expect(chart.props('labels')).toEqual(['Mar 10', 'Mar 11', 'Mar 12'])
    expect(chart.props('series')).toEqual([
      { label: 'New orders', data: [0, 2, 1] },
      { label: 'New clients', data: [4, 0, 1] },
    ])
  })

  it('shows the totals of the period', () => {
    const { newOrders, newClients } = dashboardCharts()
    const wrapper = mountWithApp(DashboardGrowthCard, { props: { newOrders, newClients, loading: false }, stubs: { UiChart: true } })

    expect(wrapper.get('[data-testid="dashboard-growth-orders"]').text()).toContain('3')
    expect(wrapper.get('[data-testid="dashboard-growth-clients"]').text()).toContain('5')
  })

  it('keeps the title row and loads the chart before the first answer', () => {
    const wrapper = mountWithApp(DashboardGrowthCard, { props: { newOrders: null, newClients: null, loading: true }, stubs: { UiChart: true } })

    expect(wrapper.text()).toContain('New orders and clients')
    expect(wrapper.getComponent(UiChart).props('loading')).toBe(true)
  })
})
