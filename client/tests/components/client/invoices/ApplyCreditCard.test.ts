/**
 * Unit tests for ClientInvoicesApplyCreditCard: it appears only while there is credit to spend and money owing on an invoice that is
 * not a top-up, caps the amount at the smaller of the two, applies it through the credit store and tells the page to read the
 * invoice again.
 *
 * @module tests/components/client/invoices/ApplyCreditCard.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { mountClient } from '~/tests/helpers/mountClient'
import ApplyCreditCard from '~/components/client/invoices/ApplyCreditCard.vue'
import type { ClientCredit } from '~/types/clientcredit'
import type { ClientInvoice } from '~/types/clientinvoice'
import { InvoiceStatus } from '~/types/invoicestatus'

/** The mocked billing endpoints the credit store reaches. */
const billingApi = vi.hoisted(() => ({
  fetchCredit: vi.fn(),
  createCreditTopUp: vi.fn(),
  applyInvoiceCredit: vi.fn()
}))

vi.mock('~/composables/apis/useBillingApi', () => ({ useBillingApi: () => billingApi }))

/**
 * Builds a credit.
 *
 * @param balance - The balance.
 * @returns The credit.
 */
const credit = (balance: number): ClientCredit => ({
  balance,
  currency: 'USD',
  topUpMinimum: 5,
  topUpMaximum: 1000,
  entries: { items: [], pageNumber: 1, pageSize: 10, totalCount: 0, totalPages: 0 }
})

/**
 * Builds an invoice.
 *
 * @param overrides - Fields to set.
 * @returns The invoice.
 */
const invoice = (overrides: Record<string, unknown> = {}): ClientInvoice => ({
  id: 41,
  status: InvoiceStatus.Unpaid,
  currency: 'USD',
  total: 100,
  amountDue: 100,
  isCreditTopUp: false,
  ...overrides
}) as unknown as ClientInvoice

/**
 * Mounts the card and waits for the credit it reads when it appears.
 *
 * @param target - The invoice to apply credit to.
 * @returns The wrapper.
 */
const mountCard = async (target: ClientInvoice) => {
  const wrapper = mountClient(ApplyCreditCard, { props: { invoice: target } })
  await flushPromises()
  return wrapper
}

describe('ClientInvoicesApplyCreditCard', () => {
  beforeEach(() => {
    billingApi.fetchCredit.mockReset()
    billingApi.applyInvoiceCredit.mockReset()
  })

  it('is shown for an owing invoice when there is credit to spend', async () => {
    billingApi.fetchCredit.mockResolvedValue(credit(60))

    const wrapper = await mountCard(invoice())

    expect(wrapper.find('[data-testid="apply-credit-card"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="apply-credit-available"]').text()).toBe('client.credit.apply.available')
  })

  it('is not shown when the balance is zero', async () => {
    billingApi.fetchCredit.mockResolvedValue(credit(0))

    const wrapper = await mountCard(invoice())

    expect(wrapper.find('[data-testid="apply-credit-card"]').exists()).toBe(false)
  })

  it('is not shown for a credit top-up invoice: credit cannot pay for credit', async () => {
    billingApi.fetchCredit.mockResolvedValue(credit(60))

    const wrapper = await mountCard(invoice({ isCreditTopUp: true }))

    expect(wrapper.find('[data-testid="apply-credit-card"]').exists()).toBe(false)
  })

  it.each([InvoiceStatus.Paid, InvoiceStatus.Cancelled, InvoiceStatus.PaymentPending, InvoiceStatus.Refunded])(
    'is not shown for an invoice that is %s',
    async (status) => {
      billingApi.fetchCredit.mockResolvedValue(credit(60))

      const wrapper = await mountCard(invoice({ status, amountDue: 0 }))

      expect(wrapper.find('[data-testid="apply-credit-card"]').exists()).toBe(false)
    }
  )

  it('fills the field with the smaller of the balance and the amount due', async () => {
    billingApi.fetchCredit.mockResolvedValue(credit(30))
    const wrapper = await mountCard(invoice({ amountDue: 100 }))

    await wrapper.get('[data-testid="apply-credit-max"]').trigger('click')

    expect((wrapper.get('[data-testid="apply-credit-amount"]').element as HTMLInputElement).value).toBe('30')
  })

  it('caps the most it offers at what the invoice still owes', async () => {
    billingApi.fetchCredit.mockResolvedValue(credit(500))
    const wrapper = await mountCard(invoice({ amountDue: 42.5 }))

    await wrapper.get('[data-testid="apply-credit-max"]').trigger('click')

    expect((wrapper.get('[data-testid="apply-credit-amount"]').element as HTMLInputElement).value).toBe('42.5')
  })

  it.each(['0', '-1', 'abc', '', '61', '10.123'])('refuses %j without asking the backend', async (value) => {
    billingApi.fetchCredit.mockResolvedValue(credit(60))
    const wrapper = await mountCard(invoice({ amountDue: 100 }))

    await wrapper.get('[data-testid="apply-credit-amount"]').setValue(value)
    await wrapper.get('form').trigger('submit')

    expect(billingApi.applyInvoiceCredit).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('client.credit.apply.amountInvalid')
  })

  it('applies the amount, reads the balance again and tells the page to read the invoice again', async () => {
    billingApi.fetchCredit.mockResolvedValueOnce(credit(60)).mockResolvedValueOnce(credit(40))
    billingApi.applyInvoiceCredit.mockResolvedValue(undefined)
    const wrapper = await mountCard(invoice())

    await wrapper.get('[data-testid="apply-credit-amount"]').setValue('20')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(billingApi.applyInvoiceCredit).toHaveBeenCalledWith(41, 20)
    expect(billingApi.fetchCredit).toHaveBeenCalledTimes(2)
    expect(wrapper.emitted('applied')).toHaveLength(1)
  })

  it('shows the backend sentence and stays quiet to the page when the application is refused', async () => {
    billingApi.fetchCredit.mockResolvedValue(credit(60))
    billingApi.applyInvoiceCredit.mockRejectedValue(new Error('refused'))
    const wrapper = await mountCard(invoice())

    await wrapper.get('[data-testid="apply-credit-amount"]').setValue('20')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.find('[data-testid="ui-form-error"]').exists()).toBe(true)
    expect(wrapper.emitted('applied')).toBeUndefined()
  })
})
