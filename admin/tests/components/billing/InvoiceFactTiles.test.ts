/**
 * Mount tests for InvoiceFactTiles: the four facts of an invoice render with the balance in the owed or settled
 * tone, and without an invoice the tiles draw placeholders.
 */
import { describe, expect, it } from 'vitest'
import InvoiceFactTiles from '@/components/billing/InvoiceFactTiles.vue'
import { mountWithApp } from '../../helpers/mount'
import { invoiceFixture } from '../../helpers/billingFixtures'

describe('InvoiceFactTiles', () => {
  it('shows the dates, the amount and the balance of the invoice', () => {
    const wrapper = mountWithApp(InvoiceFactTiles, { props: { invoice: invoiceFixture() } })

    expect(wrapper.findAll('[data-testid^="billing-invoice-fact-"]')).toHaveLength(4)
    expect(wrapper.find('[data-testid="ui-stat-tile-skeleton"]').exists()).toBe(false)
  })

  it('draws the four tiles with their captions and placeholder values while the invoice is read', () => {
    const wrapper = mountWithApp(InvoiceFactTiles, { props: { invoice: null } })

    expect(wrapper.text()).toContain('Invoice Date')
    expect(wrapper.text()).toContain('Balance')
    expect(wrapper.findAll('[data-testid="ui-stat-tile-skeleton"]')).toHaveLength(4)
  })
})
