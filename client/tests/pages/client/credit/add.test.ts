/**
 * Tests for `pages/client/credit/add.vue`: the form is held to the limits the credit reports, the top-up invoice is raised for the
 * amount, and the client is sent to pay it; a refusal stays on the page with the backend's sentence.
 *
 * @module tests/pages/client/credit/add.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { mountPage, stubClientRuntime } from '~/tests/helpers/mountClient'
import AddFundsPage from '~/pages/client/credit/add.vue'

/** The mocked billing endpoints the credit store reaches. */
const billingApi = vi.hoisted(() => ({
  fetchCredit: vi.fn(),
  createCreditTopUp: vi.fn()
}))

/** The mocked navigation. */
const navigate = vi.hoisted(() => vi.fn())

vi.mock('~/composables/apis/useBillingApi', () => ({ useBillingApi: () => billingApi }))

/** The credit every case reads: limits 5 to 100. */
const credit = {
  balance: 0,
  currency: 'USD',
  topUpMinimum: 5,
  topUpMaximum: 100,
  entries: { items: [], pageNumber: 1, pageSize: 10, totalCount: 0, totalPages: 0 }
}

describe('add funds page', () => {
  beforeEach(() => {
    billingApi.fetchCredit.mockReset()
    billingApi.createCreditTopUp.mockReset()
    navigate.mockReset()
    stubClientRuntime()
    vi.stubGlobal('navigateTo', navigate)
    billingApi.fetchCredit.mockResolvedValue(credit)
  })

  it('shows the form with the limits the backend reported', async () => {
    const wrapper = await mountPage(AddFundsPage)

    expect(wrapper.find('[data-testid="credit-add-form"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="credit-add-limits"]').exists()).toBe(true)
  })

  it('raises the top-up invoice for the amount and sends the client to pay it', async () => {
    billingApi.createCreditTopUp.mockResolvedValue(88)
    const wrapper = await mountPage(AddFundsPage)

    await wrapper.get('[data-testid="credit-add-amount"]').setValue('40')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(billingApi.createCreditTopUp).toHaveBeenCalledWith(40)
    expect(navigate).toHaveBeenCalledWith('/client/invoices/88/pay')
  })

  it('does not ask the backend for an amount outside the limits', async () => {
    const wrapper = await mountPage(AddFundsPage)

    await wrapper.get('[data-testid="credit-add-amount"]').setValue('1000')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(billingApi.createCreditTopUp).not.toHaveBeenCalled()
    expect(navigate).not.toHaveBeenCalled()
  })

  it('stays on the page and shows the sentence when the backend refuses the amount', async () => {
    billingApi.createCreditTopUp.mockRejectedValue(new Error('refused'))
    const wrapper = await mountPage(AddFundsPage)

    await wrapper.get('[data-testid="credit-add-amount"]').setValue('40')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(navigate).not.toHaveBeenCalled()
    expect(wrapper.find('[data-testid="ui-form-error"]').exists()).toBe(true)
  })

  it('shows the failure instead of a form that cannot know its limits', async () => {
    billingApi.fetchCredit.mockRejectedValue(new Error('down'))

    const wrapper = await mountPage(AddFundsPage)

    expect(wrapper.find('[data-testid="credit-add-load-error"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="credit-add-form"]').exists()).toBe(false)
  })

  it('links back to the credit page', async () => {
    const wrapper = await mountPage(AddFundsPage)

    expect(wrapper.get('[data-testid="credit-add-back"]').attributes('href')).toBe('/client/credit')
  })
})
