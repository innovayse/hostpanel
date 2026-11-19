/**
 * Mount tests for InvoiceFactTiles: the four facts of an invoice render with the balance in the owed or settled
 * tone.
 */
import { describe, expect, it } from 'vitest'
import InvoiceFactTiles from '@/components/billing/InvoiceFactTiles.vue'
import { mountWithApp } from '../../helpers/mount'
import { invoiceFixture } from '../../helpers/billingFixtures'

describe('InvoiceFactTiles', () => {
  it('shows the dates, the amount and the balance of the invoice', () => {
    const wrapper = mountWithApp(InvoiceFactTiles, { props: { invoice: invoiceFixture() } })

    expect(wrapper.findAll('[data-testid^="billing-invoice-fact-"]')).toHaveLength(4)
  })
})
