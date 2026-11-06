/**
 * Tests for `pages/client/credit/index.vue`: the balance widget, the history table, the paging of the history and the failure state.
 *
 * @module tests/pages/client/credit/index.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { mountPage, stubClientRuntime } from '~/tests/helpers/mountClient'
import CreditPage from '~/pages/client/credit/index.vue'
import { CreditMovement } from '~/types/creditmovement'

/** The mocked billing endpoints the credit store reaches. */
const billingApi = vi.hoisted(() => ({
  fetchCredit: vi.fn()
}))

vi.mock('~/composables/apis/useBillingApi', () => ({ useBillingApi: () => billingApi }))

/**
 * Builds a credit with the given lines.
 *
 * @param lines - Number of history lines on the page.
 * @param totalPages - Pages of history the backend reports.
 * @returns The credit.
 */
const credit = (lines: number, totalPages = 1) => ({
  balance: 75,
  currency: 'USD',
  topUpMinimum: 5,
  topUpMaximum: 1000,
  entries: {
    items: Array.from({ length: lines }, (_, i) => ({
      id: 100 + i,
      date: '2026-10-01T09:00:00Z',
      movement: CreditMovement.AdminAdd,
      change: 25,
      balanceAfter: 25 * (i + 1),
      invoiceId: null
    })),
    pageNumber: 1,
    pageSize: 10,
    totalCount: lines,
    totalPages
  }
})

describe('credit page', () => {
  beforeEach(() => {
    billingApi.fetchCredit.mockReset()
    stubClientRuntime()
  })

  it('reads the first page of the credit and shows the balance and the history', async () => {
    billingApi.fetchCredit.mockResolvedValue(credit(2))

    const wrapper = await mountPage(CreditPage)

    expect(billingApi.fetchCredit).toHaveBeenCalledWith(1, 10)
    expect(wrapper.get('[data-testid="credit-balance"]').text()).toContain('75.00')
    expect(wrapper.find('[data-testid="credit-entry-100"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="credit-entry-101"]').exists()).toBe(true)
  })

  it('offers the way to add funds', async () => {
    billingApi.fetchCredit.mockResolvedValue(credit(0))

    const wrapper = await mountPage(CreditPage)

    expect(wrapper.get('[data-testid="credit-add-funds"]').attributes('href')).toBe('/client/credit/add')
    expect(wrapper.find('[data-testid="credit-history-empty"]').exists()).toBe(true)
  })

  it('shows no pagination when the whole history fits one page', async () => {
    billingApi.fetchCredit.mockResolvedValue(credit(3, 1))

    const wrapper = await mountPage(CreditPage)

    expect(wrapper.find('[data-testid="ui-pagination-next"]').exists()).toBe(false)
  })

  it('reads the next page when the client asks for it', async () => {
    billingApi.fetchCredit.mockResolvedValue(credit(10, 3))
    const wrapper = await mountPage(CreditPage)
    billingApi.fetchCredit.mockClear()

    await wrapper.get('[data-testid="ui-pagination-next"]').trigger('click')
    await flushPromises()

    expect(billingApi.fetchCredit).toHaveBeenCalledWith(2, 10)
  })

  it('shows the sentence of a failed read instead of the balance', async () => {
    billingApi.fetchCredit.mockRejectedValue(new Error('down'))

    const wrapper = await mountPage(CreditPage)

    expect(wrapper.find('[data-testid="credit-error"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="credit-balance"]').exists()).toBe(false)
  })
})
