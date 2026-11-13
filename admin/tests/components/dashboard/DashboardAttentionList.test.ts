/**
 * Tests for DashboardAttentionList: a linked row per kind with its count, and the placeholder while loading.
 */
import { describe, expect, it } from 'vitest'
import DashboardAttentionList from '@/components/dashboard/DashboardAttentionList.vue'
import { mountWithApp } from '../../helpers/mount'

describe('DashboardAttentionList', () => {
  it('lists a row per kind with its count and the list it links to', () => {
    const wrapper = mountWithApp(DashboardAttentionList, {
      props: { overdueInvoices: 3, pendingOrders: 2, expiringDomains: 1, urgentTickets: 4, loading: false },
    })

    expect(wrapper.get('[data-testid="dashboard-attention-overdueInvoice"]').attributes('href')).toBe('/billing/invoices')
    expect(wrapper.get('[data-testid="dashboard-attention-overdueInvoice-count"]').text()).toBe('3')
    expect(wrapper.get('[data-testid="dashboard-attention-pendingOrder"]').attributes('href')).toBe('/orders')
    expect(wrapper.get('[data-testid="dashboard-attention-pendingOrder-count"]').text()).toBe('2')
    expect(wrapper.get('[data-testid="dashboard-attention-expiringDomain-count"]').text()).toBe('1')
    expect(wrapper.get('[data-testid="dashboard-attention-urgentTicket"]').attributes('href')).toBe('/support/tickets')
    expect(wrapper.get('[data-testid="dashboard-attention-urgentTicket-count"]').text()).toBe('4')
    expect(wrapper.text()).toContain('Needs attention')
  })

  it('keeps the rows and shows no count while the lists load', () => {
    const wrapper = mountWithApp(DashboardAttentionList, {
      props: { overdueInvoices: 0, pendingOrders: 0, expiringDomains: 0, urgentTickets: 0, loading: true },
    })

    expect(wrapper.find('[data-testid="dashboard-attention-overdueInvoice"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="dashboard-attention-overdueInvoice-count"]').exists()).toBe(false)
  })
})
