/**
 * Unit tests for ClientCreditBalanceCard: the balance in the client's currency, the way to add funds, and the dashboard form that
 * links to the credit page.
 *
 * @module tests/components/client/credit/BalanceCard.test
 */

import { describe, expect, it } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import BalanceCard from '~/components/client/credit/BalanceCard.vue'
import type { ClientCredit } from '~/types/clientcredit'

/**
 * Builds a credit with an empty history.
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

describe('ClientCreditBalanceCard', () => {
  it('shows the balance with its decimals', () => {
    const wrapper = mountClient(BalanceCard, { props: { credit: credit(12.5) } })

    expect(wrapper.get('[data-testid="credit-balance"]').text()).toContain('12.50')
  })

  it('reads a credit that is not known yet as zero', () => {
    const wrapper = mountClient(BalanceCard, { props: { credit: null } })

    expect(wrapper.get('[data-testid="credit-balance"]').text()).toContain('0.00')
  })

  it('shows a placeholder instead of a figure while the credit is read', () => {
    const wrapper = mountClient(BalanceCard, { props: { credit: null, loading: true } })

    expect(wrapper.find('[data-testid="credit-balance-loading"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="credit-balance"]').exists()).toBe(false)
  })

  it('links the add funds button to the add funds page', () => {
    const wrapper = mountClient(BalanceCard, { props: { credit: credit(0) } })

    expect(wrapper.get('[data-testid="credit-add-funds"]').attributes('href')).toBe('/client/credit/add')
  })

  it('explains how credit works on the credit page and leaves the explanation out of the dashboard widget', () => {
    const full = mountClient(BalanceCard, { props: { credit: credit(1) } })
    const compact = mountClient(BalanceCard, { props: { credit: credit(1), compact: true } })

    expect(full.find('[data-testid="credit-balance-note"]').exists()).toBe(true)
    expect(full.find('[data-testid="credit-view"]').exists()).toBe(false)
    expect(compact.find('[data-testid="credit-balance-note"]').exists()).toBe(false)
    expect(compact.get('[data-testid="credit-view"]').attributes('href')).toBe('/client/credit')
  })
})
