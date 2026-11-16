/**
 * Tests for the invoice list helpers: the paid, due and overdue rollup of a page and its printed amounts.
 */
import { describe, expect, it } from 'vitest'
import { bucketAmountText, summarizeInvoices } from '@/lib/invoices'
import { invoiceRowFixture } from '../helpers/billingFixtures'

describe('summarizeInvoices', () => {
  it('counts and sums paid, due and overdue invoices separately', () => {
    const summary = summarizeInvoices([
      invoiceRowFixture({ id: 1, status: 'Paid', total: 40, currency: 'USD' }),
      invoiceRowFixture({ id: 2, status: 'Paid', total: 10, currency: 'USD' }),
      invoiceRowFixture({ id: 3, status: 'Unpaid', total: 25, currency: 'USD' }),
      invoiceRowFixture({ id: 4, status: 'PaymentPending', total: 5, currency: 'USD' }),
      invoiceRowFixture({ id: 5, status: 'Overdue', total: 7, currency: 'USD' }),
    ])

    expect(summary.paid).toEqual({ count: 2, totals: [{ amount: 50, currency: 'USD' }] })
    expect(summary.due).toEqual({ count: 2, totals: [{ amount: 30, currency: 'USD' }] })
    expect(summary.overdue).toEqual({ count: 1, totals: [{ amount: 7, currency: 'USD' }] })
  })

  it('keeps one sum per currency and reads a missing currency as USD', () => {
    const summary = summarizeInvoices([
      invoiceRowFixture({ id: 1, status: 'Paid', total: 10, currency: 'USD' }),
      invoiceRowFixture({ id: 2, status: 'Paid', total: 500, currency: 'AMD' }),
      invoiceRowFixture({ id: 3, status: 'Paid', total: 5, currency: undefined }),
    ])

    expect(summary.paid.totals).toEqual([
      { amount: 15, currency: 'USD' },
      { amount: 500, currency: 'AMD' },
    ])
  })

  it('leaves out invoices that are neither paid, due nor overdue', () => {
    const summary = summarizeInvoices([
      invoiceRowFixture({ id: 1, status: 'Draft' }),
      invoiceRowFixture({ id: 2, status: 'Cancelled' }),
      invoiceRowFixture({ id: 3, status: 'Refunded' }),
    ])

    expect(summary.paid.count + summary.due.count + summary.overdue.count).toBe(0)
  })
})

describe('bucketAmountText', () => {
  it('prints one amount per currency', () => {
    expect(bucketAmountText({ count: 2, totals: [{ amount: 15, currency: 'USD' }, { amount: 500, currency: 'AMD' }] })).toBe('15.00 USD, 500.00 AMD')
  })

  it('prints a zero for an empty bucket', () => {
    expect(bucketAmountText({ count: 0, totals: [] })).toBe('0.00 USD')
  })
})
