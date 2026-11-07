/**
 * Unit tests for ClientDashboardCreditCard: the dashboard widget reads the credit when it appears, shows the balance with the way to
 * add funds, and shows nothing at all when the credit cannot be read.
 *
 * @module tests/components/client/dashboard/CreditCard.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { mountClient } from '~/tests/helpers/mountClient'
import CreditCard from '~/components/client/dashboard/CreditCard.vue'

/** The mocked billing endpoints the credit store reaches. */
const billingApi = vi.hoisted(() => ({ fetchCredit: vi.fn() }))

vi.mock('~/composables/apis/useBillingApi', () => ({ useBillingApi: () => billingApi }))

describe('ClientDashboardCreditCard', () => {
  beforeEach(() => {
    billingApi.fetchCredit.mockReset()
  })

  it('reads the first page of the credit and shows the balance', async () => {
    billingApi.fetchCredit.mockResolvedValue({
      balance: 25,
      currency: 'USD',
      topUpMinimum: 5,
      topUpMaximum: 1000,
      entries: { items: [], pageNumber: 1, pageSize: 10, totalCount: 0, totalPages: 0 }
    })

    const wrapper = mountClient(CreditCard)
    await flushPromises()

    expect(billingApi.fetchCredit).toHaveBeenCalledWith(1, 10)
    expect(wrapper.get('[data-testid="credit-balance"]').text()).toContain('25.00')
    expect(wrapper.find('[data-testid="credit-add-funds"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="credit-view"]').exists()).toBe(true)
  })

  it('shows nothing when the credit cannot be read', async () => {
    billingApi.fetchCredit.mockRejectedValue(new Error('down'))

    const wrapper = mountClient(CreditCard)
    await flushPromises()

    expect(wrapper.find('[data-testid="credit-balance-card"]').exists()).toBe(false)
  })
})
