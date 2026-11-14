/**
 * Tests for DashboardRecentInvoices: a row per invoice linking to it, the empty message and the loading card.
 */
import { describe, expect, it } from 'vitest'
import DashboardRecentInvoices from '@/components/dashboard/DashboardRecentInvoices.vue'
import type { Invoice } from '@/types/invoice'
import { mountWithApp } from '../../helpers/mount'

describe('DashboardRecentInvoices', () => {
  it('lists the invoices with their client, total, status and a link', () => {
    const invoice = { id: 11, clientName: 'Ada', status: 'Paid', total: 50, currency: 'USD' } as Invoice
    const wrapper = mountWithApp(DashboardRecentInvoices, { props: { invoices: [invoice], loading: false } })

    expect(wrapper.get('[data-testid="dashboard-invoice-11"]').attributes('href')).toBe('/billing/invoices/11/view')
    expect(wrapper.text()).toContain('Ada')
    expect(wrapper.text()).toContain('50')
    expect(wrapper.text()).toContain('Paid')
  })

  it('says there are no invoices once loaded', () => {
    const wrapper = mountWithApp(DashboardRecentInvoices, { props: { invoices: [], loading: false } })

    expect(wrapper.text()).toContain('No invoices yet.')
  })

  it('keeps the title row and shows placeholders while the first load runs', () => {
    const wrapper = mountWithApp(DashboardRecentInvoices, { props: { invoices: [], loading: true } })

    expect(wrapper.text()).toContain('Latest invoices')
    expect(wrapper.attributes('aria-busy')).toBe('true')
    expect(wrapper.findAll('li')).toHaveLength(5)
    expect(wrapper.findAll('li [data-testid="ui-skeleton-bar"]')).toHaveLength(20)
    expect(wrapper.text()).not.toContain('No invoices yet.')
  })
})
