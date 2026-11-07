/**
 * Tests for the transaction form helpers: the blank form, filling it from a stored transaction,
 * and reading the single linked invoice from the typed ids.
 */
import { describe, expect, it } from 'vitest'
import { blankTransactionForm, firstInvoiceId, formFromTransaction } from '@/lib/transactionForm'
import { transactionFixture } from '../helpers/billingFixtures'

describe('transactionForm', () => {
  it('starts blank, dated today', () => {
    const form = blankTransactionForm()

    expect(form.date).toBe(new Date().toISOString().split('T')[0])
    expect(form).toMatchObject({ clientId: '', amountIn: 0, amountOut: 0, fees: 0, invoiceIds: '', addToCredit: false })
  })

  it('fills from a stored transaction', () => {
    const form = formFromTransaction(transactionFixture())

    expect(form).toMatchObject({
      date: '2026-10-10',
      clientId: '7',
      description: 'Hosting payment',
      transactionId: 'ch_9',
      paymentMethod: 'Example Gateway',
      amountIn: 120,
      fees: 3.5,
      invoiceIds: '1',
    })
  })

  it('leaves the invoice ids empty when the transaction has no invoice', () => {
    expect(formFromTransaction(transactionFixture({ invoiceId: null })).invoiceIds).toBe('')
  })

  it('reads only the first invoice id', () => {
    expect(firstInvoiceId('12, 15')).toBe(12)
    expect(firstInvoiceId(' 7 ')).toBe(7)
  })

  it('reads no invoice from an empty list', () => {
    expect(firstInvoiceId('')).toBeNull()
    expect(firstInvoiceId(' , ')).toBeNull()
  })
})
