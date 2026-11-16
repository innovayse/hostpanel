/**
 * Unit tests for ClientDashboardInvoicesCard: the latest invoices and the card's states.
 *
 * @module tests/components/client/dashboard/InvoicesCard.test
 */

import { describe, expect, it } from 'vitest'
import { nextTick } from 'vue'
import { mountClient } from '~/tests/helpers/mountClient'
import InvoicesCard from '~/components/client/dashboard/InvoicesCard.vue'
import { useClientStore } from '~/stores/client'
import type { ClientInvoice } from '~/types/clientinvoice'
import { InvoiceStatus } from '~/types/invoicestatus'
import { CLIENT_INVOICES_PATH, clientInvoicePath } from '~/constants/clientRoutes'

/**
 * Builds an invoice row.
 *
 * @param id - Invoice id.
 * @returns An unpaid invoice with a due date and a total.
 */
const invoice = (id: number): ClientInvoice =>
  ({ id, status: InvoiceStatus.Unpaid, dueDate: '2026-11-01', total: 25, currency: 'USD' }) as unknown as ClientInvoice

describe('ClientDashboardInvoicesCard', () => {
  it('shows skeletons while loading', async () => {
    const wrapper = mountClient(InvoicesCard)
    useClientStore().invoicesLoading = true
    await nextTick()

    expect(wrapper.findAll('[data-testid="ui-skeleton"]')).toHaveLength(3)
  })

  it('says there are no invoices when the list is empty', () => {
    const wrapper = mountClient(InvoicesCard)

    expect(wrapper.find('[data-testid="dashboard-invoices-empty"]').text()).toBe('client.dashboard.noInvoices')
  })

  it('lists the first four invoices, each opening the invoice, with its number and status', async () => {
    const wrapper = mountClient(InvoicesCard)
    useClientStore().invoices = [1, 2, 3, 4, 5].map(invoice)
    await nextTick()

    expect(wrapper.find('[data-testid="dashboard-invoice-5"]').exists()).toBe(false)
    const row = wrapper.find('[data-testid="dashboard-invoice-1"]')
    expect(row.attributes('href')).toBe(clientInvoicePath(1))
    expect(row.text()).toContain('#1')
    expect(row.find('[data-testid="status-badge"]').exists()).toBe(true)
  })

  it('links the header button to the full list', () => {
    const wrapper = mountClient(InvoicesCard)

    expect(wrapper.find('[data-testid="dashboard-invoices-all"]').attributes('href')).toBe(CLIENT_INVOICES_PATH)
  })
})
