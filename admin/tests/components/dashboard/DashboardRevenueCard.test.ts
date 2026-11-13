/**
 * Tests for DashboardRevenueCard: the chart gets the months of the chosen period and the average as its target.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import DashboardRevenueCard from '@/components/dashboard/DashboardRevenueCard.vue'
import UiChart from '@/components/ui/UiChart.vue'
import { mountWithApp } from '../../helpers/mount'

/** Income of the first four months. */
const income = [
  { month: 'Jan', amount: 100, currency: 'USD' },
  { month: 'Feb', amount: 300, currency: 'USD' },
  { month: 'Mar', amount: 200, currency: 'USD' },
  { month: 'Apr', amount: 400, currency: 'USD' },
]

describe('DashboardRevenueCard', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-04-15T10:00:00'))
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('hands the last two months and a target line to the chart for 30 days', () => {
    const wrapper = mountWithApp(DashboardRevenueCard, { props: { income, currency: 'USD', period: '30', loading: false }, stubs: { UiChart: true } })
    const chart = wrapper.getComponent(UiChart)

    expect(chart.props('height')).toBe('short')

    expect(chart.props('labels')).toEqual(['Mar', 'Apr'])
    expect(chart.props('series')).toEqual([{ label: 'Income', data: [200, 400] }])
    expect(chart.props('target')).toBe(250)
    expect(wrapper.text()).toContain('Revenue against target')
  })

  it('reaches four months back for 90 days', () => {
    const wrapper = mountWithApp(DashboardRevenueCard, { props: { income, currency: 'USD', period: '90', loading: false }, stubs: { UiChart: true } })

    expect(wrapper.getComponent(UiChart).props('labels')).toEqual(['Jan', 'Feb', 'Mar', 'Apr'])
  })

  it('passes the loading state to the chart', () => {
    const wrapper = mountWithApp(DashboardRevenueCard, { props: { income: [], currency: 'USD', period: '30', loading: true }, stubs: { UiChart: true } })

    expect(wrapper.getComponent(UiChart).props('loading')).toBe(true)
  })
})
