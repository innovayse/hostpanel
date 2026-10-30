/**
 * Mount tests for InvoiceTransactionsSection: the payments ledger, the full history with its
 * type badges, and the empty message.
 */
import { describe, expect, it } from 'vitest'
import InvoiceTransactionsSection from '@/components/billing/InvoiceTransactionsSection.vue'
import { invoiceFixture } from '../../helpers/billingFixtures'
import { mountWithApp } from '../../helpers/mount'

describe('InvoiceTransactionsSection', () => {
  it('lists only the payments in the first ledger and everything in the history', () => {
    const wrapper = mountWithApp(InvoiceTransactionsSection, { props: { invoice: invoiceFixture() } })

    expect(wrapper.findAll('[data-testid="billing-invoice-payments-table"] tbody tr')).toHaveLength(1)
    expect(wrapper.findAll('[data-testid="billing-invoice-history-table"] tbody tr')).toHaveLength(2)
    expect(wrapper.get('[data-testid="billing-invoice-history-table"]').text()).toContain('Goodwill')
    expect(wrapper.get('[data-testid="billing-invoice-payments-table"]').text()).toContain('40.00 USD')
  })

  it('formats payment amounts and fees with the invoice currency when not USD', () => {
    const wrapper = mountWithApp(InvoiceTransactionsSection, {
      props: {
        invoice: invoiceFixture({
          currency: 'AMD',
          transactions: [
            {
              id: 21,
              date: '2026-10-15T00:00:00Z',
              gateway: 'Stripe',
              transactionId: 'ch_1',
              amount: 24000,
              fees: 500,
              type: 'Payment',
            },
          ],
        }),
      },
    })

    const paymentsTable = wrapper.get('[data-testid="billing-invoice-payments-table"]')
    expect(paymentsTable.text()).toContain('24000.00 AMD')
    expect(paymentsTable.text()).toContain('500.00 AMD')
    expect(paymentsTable.text()).not.toContain('$')
  })

  it('shows the empty message in both ledgers when there are no transactions', () => {
    const wrapper = mountWithApp(InvoiceTransactionsSection, { props: { invoice: invoiceFixture({ transactions: [] }) } })

    expect(wrapper.get('[data-testid="billing-invoice-payments-table"]').text()).toContain('No Records Found')
    expect(wrapper.get('[data-testid="billing-invoice-history-table"]').text()).toContain('No Records Found')
  })
})
