/**
 * Tests for DashboardTicketPriorityCard: one bar per priority, the highest first, zero priorities kept.
 */
import { describe, expect, it } from 'vitest'
import DashboardTicketPriorityCard from '@/components/dashboard/DashboardTicketPriorityCard.vue'
import UiChart from '@/components/ui/UiChart.vue'
import { dashboardCharts } from '../../helpers/dashboardFixtures'
import { mountWithApp } from '../../helpers/mount'

describe('DashboardTicketPriorityCard', () => {
  it('draws one bar per priority, a priority with no ticket as zero', () => {
    const wrapper = mountWithApp(DashboardTicketPriorityCard, {
      props: { priorities: dashboardCharts().openTickets, loading: false },
      stubs: { UiChart: true },
    })
    const chart = wrapper.getComponent(UiChart)

    expect(chart.props('labels')).toEqual(['High', 'Medium', 'Low'])
    expect(chart.props('series')).toEqual([{ label: 'Open tickets', data: [2, 5, 0] }])
  })

  it('keeps the title row and loads the chart before the first answer', () => {
    const wrapper = mountWithApp(DashboardTicketPriorityCard, { props: { priorities: null, loading: true }, stubs: { UiChart: true } })

    expect(wrapper.text()).toContain('Open tickets by priority')
    expect(wrapper.getComponent(UiChart).props('loading')).toBe(true)
  })
})
