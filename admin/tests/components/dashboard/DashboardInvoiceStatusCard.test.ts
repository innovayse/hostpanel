/**
 * Tests for DashboardInvoiceStatusCard: one bar per status, and a row with the count and the amount under the chart.
 */
import { describe, expect, it } from 'vitest'
import DashboardInvoiceStatusCard from '@/components/dashboard/DashboardInvoiceStatusCard.vue'
import UiChart from '@/components/ui/UiChart.vue'
import { dashboardCharts } from '../../helpers/dashboardFixtures'
import { mountWithApp } from '../../helpers/mount'

describe('DashboardInvoiceStatusCard', () => {
  it('draws one bar per status in the order the API sent them', () => {
    const wrapper = mountWithApp(DashboardInvoiceStatusCard, {
      props: { statuses: dashboardCharts().invoicesByStatus, currency: 'USD', loading: false },
      stubs: { UiChart: true },
    })
    const chart = wrapper.getComponent(UiChart)

    expect(chart.props('type')).toBe('bar')
    expect(chart.props('labels')).toEqual(['Paid', 'Unpaid', 'Overdue', 'Cancelled'])
    expect(chart.props('series')).toEqual([{ label: 'Invoices', data: [7, 3, 2, 1] }])
  })

  it('lists the count and the amount of each status', () => {
    const wrapper = mountWithApp(DashboardInvoiceStatusCard, {
      props: { statuses: dashboardCharts().invoicesByStatus, currency: 'USD', loading: false },
      stubs: { UiChart: true },
    })

    expect(wrapper.get('[data-testid="dashboard-invoice-status-Paid"]').text()).toContain('7 · $700')
    expect(wrapper.get('[data-testid="dashboard-invoice-status-Overdue"]').text()).toContain('2 · $200')
  })

  it('keeps the title row and loads the chart before the first answer', () => {
    const wrapper = mountWithApp(DashboardInvoiceStatusCard, { props: { statuses: null, currency: '', loading: true }, stubs: { UiChart: true } })

    expect(wrapper.text()).toContain('Invoices by status')
    expect(wrapper.getComponent(UiChart).props('loading')).toBe(true)
  })
})
