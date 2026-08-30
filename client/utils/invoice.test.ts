/**
 * Tests for the invoice derivations in `utils/invoice.ts`.
 *
 * The anchor case is the production bug these were extracted to fix: the payment page computed
 * both money figures from `invoice.balance`, a field `InvoiceDto` has never sent. `balance ??
 * total` always took the second branch, so **Payments to date rendered `0.00` on every invoice
 * and Balance Due rendered the full total on an invoice already part-paid** — on the screen
 * that then asks the customer to pay it. The part-paid case is therefore the first test here,
 * not an edge case.
 *
 * `isInvoiceOverdue` takes its `now` as an argument precisely so these assertions do not drift
 * into flakiness the day someone runs them near midnight.
 *
 * @module utils/invoice.test
 */

import { describe, expect, it } from 'vitest'
import { balanceDue, isInvoiceOverdue, paymentsToDate } from './invoice'
import type { ClientInvoice, ClientInvoiceTransaction } from '~/types/clientinvoice'

/** A transaction carrying only the fields these functions read. */
const txn = (amount: number, fees = 0): ClientInvoiceTransaction => ({
  id: 1,
  date: '2026-08-01T10:00:00+00:00',
  gateway: 'stripe',
  transactionId: 'ch_1',
  amount,
  fees,
  type: 'Payment',
  notes: null
})

/** A minimal invoice; every test overrides the fields it cares about. */
const invoice = (over: Partial<ClientInvoice> = {}): ClientInvoice => ({
  id: 8,
  clientId: 1,
  clientName: 'Test Client',
  status: 'Unpaid',
  invoiceDate: '2026-08-01T00:00:00+00:00',
  dueDate: '2026-08-15T00:00:00+00:00',
  createdAt: '2026-08-01T00:00:00+00:00',
  paidAt: null,
  total: 20000,
  subTotal: 20000,
  tax: 0,
  taxRate: 0,
  credit: 0,
  gatewayTransactionId: null,
  notes: null,
  paymentMethod: null,
  items: [],
  transactions: [],
  ...over
})

describe('paymentsToDate', () => {
  it('sums the transactions actually recorded against the invoice', () => {
    expect(paymentsToDate([txn(5000), txn(3000)])).toBe(8000)
  })

  it('subtracts a refund, which is recorded as a negative amount', () => {
    expect(paymentsToDate([txn(5000), txn(-2000)])).toBe(3000)
  })

  it('ignores gateway fees, which are the gateway\'s cut and not the customer\'s money', () => {
    // The customer handed over 5000; that 150 came out of the merchant's side of the ledger.
    expect(paymentsToDate([txn(5000, 150)])).toBe(5000)
  })

  it('reads an absent or empty transaction list as nothing received', () => {
    expect(paymentsToDate([])).toBe(0)
    expect(paymentsToDate(null)).toBe(0)
    expect(paymentsToDate(undefined)).toBe(0)
  })
})

describe('balanceDue', () => {
  it('reports what is left on a part-paid invoice — the bug this replaced', () => {
    // The whole reason this file exists. `balance ?? total` rendered the full 20000 here,
    // on the screen that then asks the customer to pay.
    const partPaid = invoice({ total: 20000, transactions: [txn(5000)] })

    expect(paymentsToDate(partPaid.transactions)).toBe(5000)
    expect(balanceDue(partPaid)).toBe(15000)
  })

  it('reports nothing outstanding on a fully paid invoice', () => {
    expect(balanceDue(invoice({ status: 'Paid', total: 20000, transactions: [txn(20000)] }))).toBe(0)
  })

  it('reports the full total when nothing has been received', () => {
    expect(balanceDue(invoice({ total: 20000 }))).toBe(20000)
  })

  it('reports zero rather than NaN while the invoice is still loading', () => {
    expect(balanceDue(null)).toBe(0)
    expect(balanceDue(undefined)).toBe(0)
  })
})

describe('isInvoiceOverdue', () => {
  /** A fixed "today", so nothing here depends on when the suite runs. */
  const now = new Date('2026-08-20T12:00:00Z')

  it('treats the backend\'s own Overdue status as overdue', () => {
    expect(isInvoiceOverdue(invoice({ status: 'Overdue' }), now)).toBe(true)
  })

  it('treats an unpaid invoice past its due date as overdue before the cron catches up', () => {
    // `ProcessRenewalsCronHandler` writes the `Overdue` status, so there is a window in which
    // the invoice is late on the customer's calendar but still reads `Unpaid`.
    expect(isInvoiceOverdue(invoice({ status: 'Unpaid', dueDate: '2026-08-15T00:00:00+00:00' }), now)).toBe(true)
  })

  it('does not call an unpaid invoice overdue before its due date', () => {
    expect(isInvoiceOverdue(invoice({ status: 'Unpaid', dueDate: '2026-08-25T00:00:00+00:00' }), now)).toBe(false)
  })

  it('never calls a paid, draft, cancelled or refunded invoice overdue, whatever its date', () => {
    // A past due date on any of these is not a debt — the invoice was paid, never issued,
    // voided, or already returned.
    for (const status of ['Paid', 'Draft', 'Cancelled', 'Refunded', 'Collections'] as const) {
      expect(isInvoiceOverdue(invoice({ status, dueDate: '2026-01-01T00:00:00+00:00' }), now)).toBe(false)
    }
  })

  it('does not treat an unparseable due date as evidence of lateness', () => {
    expect(isInvoiceOverdue(invoice({ status: 'Unpaid', dueDate: 'not a date' }), now)).toBe(false)
  })

  it('reports false rather than throwing while the invoice is still loading', () => {
    expect(isInvoiceOverdue(null, now)).toBe(false)
    expect(isInvoiceOverdue(undefined, now)).toBe(false)
  })
})
