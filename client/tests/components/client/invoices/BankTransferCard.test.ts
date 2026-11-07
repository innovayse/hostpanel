/**
 * Unit tests for BankTransferCard: the reference stands out, every bank detail the operator filled in has a copy button, a
 * detail left empty is not shown, the amount is formatted in the invoice currency, and no client id appears anywhere.
 */
import { describe, expect, it } from 'vitest'
import { mountClient, stubClientRuntime } from '~/tests/helpers/mountClient'
import BankTransferCard from '~/components/client/invoices/BankTransferCard.vue'
import type { BankTransferDetails } from '~/types/banktransferdetails'

/** A transfer with every detail filled in. */
const full: BankTransferDetails = {
  invoiceId: 9,
  reference: 'INV-9',
  amountDue: 120.5,
  currency: 'USD',
  accountName: 'Example Hosting LLC',
  iban: 'AM00 1111 2222',
  swift: 'EXAMAMXX',
  bankName: 'Example Bank',
  instructions: 'Pay within five days.'
}

/**
 * Mounts the card for a set of details.
 *
 * @param details - The details to show.
 * @returns The wrapper.
 */
const mountCard = (details: BankTransferDetails) => {
  stubClientRuntime()
  return mountClient(BankTransferCard, { props: { details } })
}

describe('BankTransferCard', () => {
  it('shows the reference prominently with a copy button', () => {
    const wrapper = mountCard(full)

    expect(wrapper.get('[data-testid="bank-transfer-reference"]').text()).toBe('INV-9')
    expect(wrapper.find('[data-testid="bank-transfer-copy-reference"]').exists()).toBe(true)
  })

  it('shows the awaiting-confirmation status', () => {
    const wrapper = mountCard(full)

    expect(wrapper.get('[data-testid="bank-transfer-status"]').text()).toContain('invoicePay.bankTransfer.awaiting')
  })

  it('shows every filled-in detail with its own copy button', () => {
    const wrapper = mountCard(full)

    expect(wrapper.get('[data-testid="bank-transfer-iban"]').text()).toBe('AM00 1111 2222')
    expect(wrapper.get('[data-testid="bank-transfer-swift"]').text()).toBe('EXAMAMXX')
    expect(wrapper.get('[data-testid="bank-transfer-accountName"]').text()).toBe('Example Hosting LLC')
    expect(wrapper.get('[data-testid="bank-transfer-bankName"]').text()).toBe('Example Bank')
    for (const key of ['amount', 'accountName', 'bankName', 'iban', 'swift']) {
      expect(wrapper.find(`[data-testid="bank-transfer-copy-${key}"]`).exists()).toBe(true)
    }
    expect(wrapper.get('[data-testid="bank-transfer-instructions"]').text()).toContain('Pay within five days.')
  })

  it('formats the amount in the invoice currency', () => {
    const wrapper = mountCard(full)

    expect(wrapper.get('[data-testid="bank-transfer-amount"]').text()).toContain('120.50')
  })

  it('leaves out the details the operator left empty', () => {
    const wrapper = mountCard({ ...full, accountName: null, swift: null, bankName: null, instructions: null })

    expect(wrapper.find('[data-testid="bank-transfer-row-accountName"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="bank-transfer-row-swift"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="bank-transfer-row-bankName"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="bank-transfer-instructions"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="bank-transfer-row-iban"]').exists()).toBe(true)
  })

  it('tells the payer to contact the provider when every account detail and the instructions are empty', () => {
    const wrapper = mountCard({ ...full, accountName: null, iban: null, swift: null, bankName: null, instructions: null })

    expect(wrapper.get('[data-testid="bank-transfer-no-details"]').text()).toContain('invoicePay.bankTransfer.noDetails')
    expect(wrapper.get('[data-testid="bank-transfer-reference"]').text()).toBe('INV-9')
  })

  it('shows no contact notice while any account detail or the instructions are set', () => {
    expect(mountCard(full).find('[data-testid="bank-transfer-no-details"]').exists()).toBe(false)
    const onlyInstructions = mountCard({ ...full, accountName: null, iban: null, swift: null, bankName: null })
    expect(onlyInstructions.find('[data-testid="bank-transfer-no-details"]').exists()).toBe(false)
  })

  it('shows no client id', () => {
    const wrapper = mountCard(full)

    expect(wrapper.html()).not.toMatch(/client/i)
  })
})
