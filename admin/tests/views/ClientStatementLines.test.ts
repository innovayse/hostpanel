/**
 * Tests of ClientStatementView with data: an invoice line is rendered from its typed kind and invoice id through
 * i18n, a transaction line shows the description the API sent, and a history payment line names its invoice.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { ReportStatementLineType } from '@/types/reportclientstatement'
import ClientStatementView from '@/views/ClientStatementView.vue'
import { json } from '../helpers/fetch'
import { mountView } from '../helpers/mount'
import { stubRouteTable } from '../helpers/routes'

vi.mock('chart.js', async () => (await import('../helpers/chartJsMock')).chartJsMock)

describe('ClientStatementView lines', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('renders the invoice text from the line kind and invoice id, and the transaction description as sent', async () => {
    stubRouteTable({
      'GET /reports/client-picker': () => json([{ id: 1, name: 'Acme' }]),
      'GET /reports/client-statement': () => json({
        clientId: 1,
        clientName: 'Acme',
        balances: [
          { currency: 'EUR', previousBalance: 0, endingBalance: 0 },
          { currency: 'USD', previousBalance: 0, endingBalance: 5 },
        ],
        lines: [
          { type: ReportStatementLineType.Invoice, date: '2026-01-01', invoiceId: 42, description: null, currency: 'USD', debit: 15, credit: 0, balance: 15 },
          { type: ReportStatementLineType.Transaction, date: '2026-01-02', invoiceId: null, description: 'Card payment', currency: 'USD', debit: 0, credit: 10, balance: 5 },
          { type: ReportStatementLineType.PaidHistory, date: '2026-01-03', invoiceId: 43, description: null, currency: 'EUR', debit: 0, credit: 5, balance: 0 },
        ],
      }),
    })
    const wrapper = await mountView(ClientStatementView)
    ;(wrapper.vm as unknown as { selectedClient: number }).selectedClient = 1

    await wrapper.get('[data-testid="report-generate"]').trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Invoice Payment #42')
    expect(wrapper.text()).toContain('Card payment')
    expect(wrapper.text()).toContain('Invoice #43, paid before import')
    expect(wrapper.get('[data-testid="report-ending-balance"]').text()).toContain('€0.00')
    expect(wrapper.get('[data-testid="report-ending-balance"]').text()).toContain('$5.00')
  })
})
