/**
 * Tests for the transaction filters every transaction list shares: the address is read back as the typed direction,
 * payment method, period and amount the API takes, a value that is not one is ignored, and each applied filter is
 * described for its chip.
 */
import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { useListQuery } from '@/composables/useListQuery'
import { TransactionFilterKey, useTransactionFilters } from '@/composables/useTransactionFilters'
import type { TransactionFilterSet } from '@/composables/useTransactionFilters'
import { TransactionDirection } from '@/types/transaction'
import { createTestI18n } from '../helpers/i18n'

/**
 * Mounts a screen that uses the composables, at the given address.
 *
 * @param url - The address the router starts at.
 * @returns A reader of the transaction filters the screen holds.
 */
const setup = async (url: string) => {
  let state: TransactionFilterSet | null = null
  const Screen = defineComponent({
    setup() {
      const list = useListQuery(Object.values(TransactionFilterKey))
      state = useTransactionFilters(list)
      return () => h('div')
    },
  })
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/transactions', component: Screen }] })
  await router.push(url)
  await router.isReady()
  mount({ render: () => h('div', [h(Screen)]) }, { global: { plugins: [router, createTestI18n()] } })
  await flushPromises()
  return () => state as unknown as TransactionFilterSet
}

describe('useTransactionFilters', () => {
  it('reads the direction, the payment method, the period and the amount from the address as the API takes them', async () => {
    const filters = await setup('/transactions?direction=Out&paymentMethod=Bank%20Transfer&from=2026-03-01&to=2026-03-31&amount=12.5')

    expect(filters().filters.value).toEqual({
      direction: TransactionDirection.Out,
      paymentMethod: 'Bank Transfer',
      from: '2026-03-01',
      to: '2026-03-31',
      amount: 12.5,
    })
  })

  it('leaves out a filter that is not set or holds a value that is not a member, a date or a positive amount', async () => {
    const filters = await setup('/transactions?direction=Sideways&from=soon&to=later&amount=-3')

    expect(filters().filters.value).toEqual({
      direction: undefined,
      paymentMethod: undefined,
      from: undefined,
      to: undefined,
      amount: undefined,
    })
  })

  it('ignores an amount that is not a number', async () => {
    const filters = await setup('/transactions?amount=abc')

    expect(filters().filters.value.amount).toBeUndefined()
  })

  it('describes each applied filter for its chip, naming a known payment method and showing another as written', async () => {
    const known = await setup('/transactions?direction=In&paymentMethod=Card&from=2026-03-01&to=2026-03-31&amount=40')
    const other = await setup('/transactions?paymentMethod=Bank%20Transfer')

    expect(known().chipSources().map((source) => [source.key, source.label])).toEqual([
      ['direction', 'Show'],
      ['paymentMethod', 'Payment Method'],
      ['from', 'Date from'],
      ['to', 'Date to'],
      ['amount', 'Amount'],
    ])
    expect(known().chipSources().find((source) => source.key === 'direction')?.value).toBe('Payments Received')
    expect(known().chipSources().find((source) => source.key === 'paymentMethod')?.value).toBe('Credit/Debit Card')
    expect(known().chipSources().find((source) => source.key === 'amount')?.value).toBe('40')
    expect(other().chipSources().find((source) => source.key === 'paymentMethod')?.value).toBe('Bank Transfer')
  })

  it('describes a filter that is not set without a value', async () => {
    const filters = await setup('/transactions')

    expect(filters().chipSources().every((source) => source.value === undefined)).toBe(true)
  })
})
