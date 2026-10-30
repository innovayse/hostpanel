/**
 * Tests for the invoice derivations in `utils/invoice.ts`.
 *
 * The anchor case is the production bug these were extracted to fix: the payment page computed
 * both money figures from `invoice.balance`, a field `InvoiceDto` has never sent. `balance ??
 * total` always took the second branch, so **Payments to date rendered `0.00` on every invoice
 * and Balance Due rendered the full total on an invoice already part-paid**: on the screen
 * that then asks the customer to pay it. The part-paid case is therefore the first test here,
 * not an edge case.
 *
 * `isInvoiceOverdue` takes its `now` as an argument precisely so these assertions do not drift
 * into flakiness the day someone runs them near midnight.
 *
 * @module tests/utils/invoice.test
 */

import { describe, expect, it } from 'vitest'
import { balanceDue, isInvoiceOutstanding, isInvoiceOverdue, isInvoicePaid, paymentsToDate } from '~/utils/invoice'
import type { ClientInvoice } from '~/types/clientinvoice'
import type { ClientInvoiceTransaction } from '~/types/clientinvoicetransaction'
import { InvoiceStatus } from '~/types/invoicestatus'
import { InvoiceTransactionType } from '~/types/invoicetransactiontype'

/**
 * A transaction carrying only the fields these functions read.
 *
 * @param amount - The amount, positive as the API stores every kind.
 * @param type - Which way the money moved; a payment unless stated.
 * @returns The transaction.
 */
const txn = (amount: number, type: InvoiceTransactionType = InvoiceTransactionType.Payment): ClientInvoiceTransaction => ({
  id: 1,
  date: '2026-08-01T10:00:00+00:00',
  amount,
  type
})

/**
 * A minimal invoice; every test overrides the fields it cares about.
 *
 * @param over - The fields to override.
 * @returns The invoice.
 */
const invoice = (over: Partial<ClientInvoice> = {}): ClientInvoice => ({
  id: 8,
  status: InvoiceStatus.Unpaid,
  currency: 'USD',
  invoiceDate: '2026-08-01T00:00:00+00:00',
  dueDate: '2026-08-15T00:00:00+00:00',
  createdAt: '2026-08-01T00:00:00+00:00',
  paidAt: null,
  total: 20000,
  subTotal: 20000,
  tax: 0,
  taxRate: 0,
  credit: 0,
  amountDue: 20000,
  paymentMethod: null,
  items: [],
  transactions: [],
  ...over
})

describe('paymentsToDate', () => {
  it('sums the transactions actually recorded against the invoice', () => {
    expect(paymentsToDate([txn(5000), txn(3000)])).toBe(8000)
  })

  it('counts only payments, never a refund or a credit, which are stored as positive amounts', () => {
    expect(paymentsToDate([
      txn(5000),
      txn(2000, InvoiceTransactionType.Refund),
      txn(1000, InvoiceTransactionType.Credit)
    ])).toBe(5000)
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
    const partPaid = invoice({ total: 20000, amountDue: 15000, transactions: [txn(5000)] })

    expect(paymentsToDate(partPaid.transactions)).toBe(5000)
    expect(balanceDue(partPaid)).toBe(15000)
  })

  it('reports the amount due the API computed, which is what a payment charges', () => {
    // The server's figure is the one the gateway is asked to charge; a page that worked out its
    // own could show the customer one amount and charge another.
    expect(balanceDue(invoice({ total: 20000, amountDue: 12000, transactions: [] }))).toBe(12000)
  })

  it('reports nothing outstanding on a fully paid invoice', () => {
    expect(balanceDue(invoice({ status: InvoiceStatus.Paid, total: 20000, amountDue: 0, transactions: [txn(20000)] }))).toBe(0)
  })

  it('reads the API figure after a refund instead of counting the refund as money received', () => {
    // A refund must not raise the paid figure, which could zero the balance and hide the pay panel.
    const refunded = invoice({ total: 20000, amountDue: 15000, transactions: [txn(5000), txn(5000, InvoiceTransactionType.Refund)] })

    expect(paymentsToDate(refunded.transactions)).toBe(5000)
    expect(balanceDue(refunded)).toBe(15000)
  })

  it('reports the full total when nothing has been received', () => {
    expect(balanceDue(invoice({ total: 20000 }))).toBe(20000)
  })

  it('reports zero rather than NaN while the invoice is still loading', () => {
    expect(balanceDue(null)).toBe(0)
    expect(balanceDue(undefined)).toBe(0)
  })
})

describe('isInvoiceOutstanding', () => {
  it('counts an invoice the cron has already marked Overdue', () => {
    // The case the dashboard was dropping. Overdue is what Unpaid becomes, not a separate
    // kind of debt, and it is the more urgent half of what the customer owes.
    expect(isInvoiceOutstanding(invoice({ status: InvoiceStatus.Overdue }))).toBe(true)
  })

  it('counts an invoice that is still Unpaid', () => {
    expect(isInvoiceOutstanding(invoice({ status: InvoiceStatus.Unpaid }))).toBe(true)
  })

  it('agrees with the banner and the stat card, which is the bug it exists for', () => {
    // Six Unpaid and six Overdue on one account must read "12" on the card and in the banner
    // beside it alike: one predicate over the same list is the only answer available.
    const invoices = [
      ...Array.from({ length: 6 }, () => invoice({ status: InvoiceStatus.Unpaid })),
      ...Array.from({ length: 6 }, () => invoice({ status: InvoiceStatus.Overdue })),
      ...Array.from({ length: 3 }, () => invoice({ status: InvoiceStatus.Paid })),
    ]

    expect(invoices.filter(isInvoiceOutstanding)).toHaveLength(12)
  })

  it('does not count a status with nothing owing on it', () => {
    // Draft was never issued, Cancelled was voided, Refunded was returned, Paid is settled, and a
    // partly refunded invoice was settled before part of it went back.
    for (const status of [InvoiceStatus.Paid, InvoiceStatus.Draft, InvoiceStatus.Cancelled, InvoiceStatus.Refunded, InvoiceStatus.PartiallyRefunded]) {
      expect(isInvoiceOutstanding(invoice({ status }))).toBe(false)
    }
  })
})

describe('isInvoicePaid', () => {
  it('counts a paid invoice and a partially refunded one as paid', () => {
    expect(isInvoicePaid(invoice({ status: InvoiceStatus.Paid }))).toBe(true)
    expect(isInvoicePaid(invoice({ status: InvoiceStatus.PartiallyRefunded }))).toBe(true)
  })

  it('does not count a fully refunded invoice, nor one that is not settled', () => {
    // A Refunded invoice gave all of its money back; the rest never took any.
    for (const status of [
      InvoiceStatus.Refunded,
      InvoiceStatus.Unpaid,
      InvoiceStatus.Overdue,
      InvoiceStatus.Draft,
      InvoiceStatus.Cancelled,
      InvoiceStatus.Collections,
      InvoiceStatus.PaymentPending
    ]) {
      expect(isInvoicePaid(invoice({ status }))).toBe(false)
    }
  })
})

describe('isInvoiceOverdue', () => {
  /** A fixed "today", so nothing here depends on when the suite runs. */
  const now = new Date('2026-08-20T12:00:00Z')

  it('treats the backend\'s own Overdue status as overdue', () => {
    expect(isInvoiceOverdue(invoice({ status: InvoiceStatus.Overdue }), now)).toBe(true)
  })

  it('treats an unpaid invoice past its due date as overdue before the cron catches up', () => {
    // `ProcessRenewalsCronHandler` writes the `Overdue` status, so there is a window in which
    // the invoice is late on the customer's calendar but still reads `Unpaid`.
    expect(isInvoiceOverdue(invoice({ status: InvoiceStatus.Unpaid, dueDate: '2026-08-15T00:00:00+00:00' }), now)).toBe(true)
  })

  it('does not call an unpaid invoice overdue before its due date', () => {
    expect(isInvoiceOverdue(invoice({ status: InvoiceStatus.Unpaid, dueDate: '2026-08-25T00:00:00+00:00' }), now)).toBe(false)
  })

  it('never calls a paid, draft, cancelled or refunded invoice overdue, whatever its date', () => {
    // A past due date on any of these is not a debt: the invoice was paid, never issued,
    // voided, or already returned.
    for (const status of [
      InvoiceStatus.Paid,
      InvoiceStatus.Draft,
      InvoiceStatus.Cancelled,
      InvoiceStatus.Refunded,
      InvoiceStatus.PartiallyRefunded,
      InvoiceStatus.Collections
    ]) {
      expect(isInvoiceOverdue(invoice({ status, dueDate: '2026-01-01T00:00:00+00:00' }), now)).toBe(false)
    }
  })

  it('does not treat an unparseable due date as evidence of lateness', () => {
    expect(isInvoiceOverdue(invoice({ status: InvoiceStatus.Unpaid, dueDate: 'not a date' }), now)).toBe(false)
  })

  it('reports false rather than throwing while the invoice is still loading', () => {
    expect(isInvoiceOverdue(null, now)).toBe(false)
    expect(isInvoiceOverdue(undefined, now)).toBe(false)
  })
})
