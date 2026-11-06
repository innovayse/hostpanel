/**
 * Tests for `stores/credit.ts`.
 *
 * The store, not the credit pages, owns the loaded credit, the in-flight flags and the refusal sentences, so these assertions pin
 * the contract the pages render from.
 *
 * @module tests/stores/credit.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { CREDIT_PAGE_SIZE } from '~/stores/credit'
import type { ClientCredit } from '~/types/clientcredit'

/** The mocked credit read. */
const fetchCreditMock = vi.fn()

/** The mocked top-up invoice creation. */
const createTopUpMock = vi.fn()

/** The mocked application of credit to an invoice. */
const applyCreditMock = vi.fn()

vi.mock('~/composables/apis/useBillingApi', () => ({
  /**
   * Stands in for the real API composable so the store runs without transport.
   *
   * @returns Only the endpoints the credit store reaches for.
   */
  useBillingApi: () => ({
    fetchCredit: fetchCreditMock,
    createCreditTopUp: createTopUpMock,
    applyInvoiceCredit: applyCreditMock
  })
}))

const { useCreditStore } = await import('~/stores/credit')

/**
 * Builds a credit.
 *
 * @param balance - The balance.
 * @returns The credit with an empty history.
 */
const credit = (balance: number): ClientCredit => ({
  balance,
  currency: 'USD',
  topUpMinimum: 5,
  topUpMaximum: 1000,
  entries: { items: [], pageNumber: 1, pageSize: CREDIT_PAGE_SIZE, totalCount: 0, totalPages: 0 }
})

describe('useCreditStore.load', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    fetchCreditMock.mockReset()
  })

  it('reads the credit page and exposes the balance', async () => {
    fetchCreditMock.mockResolvedValue(credit(12.5))
    const store = useCreditStore()

    const loaded = await store.load(1)

    expect(fetchCreditMock).toHaveBeenCalledWith(1, CREDIT_PAGE_SIZE)
    expect(loaded?.balance).toBe(12.5)
    expect(store.balance).toBe(12.5)
    expect(store.hasCredit).toBe(true)
    expect(store.loading).toBe(false)
    expect(store.error).toBe('')
  })

  it('remembers the page it read and asks for it again when no page is given', async () => {
    fetchCreditMock.mockResolvedValue(credit(0))
    const store = useCreditStore()

    await store.load(3)
    await store.load()

    expect(fetchCreditMock).toHaveBeenLastCalledWith(3, CREDIT_PAGE_SIZE)
    expect(store.page).toBe(3)
  })

  it('reads a zero balance as no credit to spend', async () => {
    fetchCreditMock.mockResolvedValue(credit(0))
    const store = useCreditStore()

    await store.load(1)

    expect(store.hasCredit).toBe(false)
  })

  it('keeps the sentence, drops the stale credit and releases the flag when the read fails', async () => {
    fetchCreditMock.mockResolvedValueOnce(credit(9)).mockRejectedValueOnce(new Error('down'))
    const store = useCreditStore()
    await store.load(1)

    const loaded = await store.load(1)

    expect(loaded).toBeNull()
    expect(store.credit).toBeNull()
    expect(store.balance).toBe(0)
    expect(store.error).not.toBe('')
    expect(store.loading).toBe(false)
  })
})

describe('useCreditStore.createTopUp', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    createTopUpMock.mockReset()
  })

  it('returns the id of the new invoice and releases the flag', async () => {
    createTopUpMock.mockResolvedValue(88)
    const store = useCreditStore()

    const invoiceId = await store.createTopUp(40)

    expect(invoiceId).toBe(88)
    expect(createTopUpMock).toHaveBeenCalledWith(40)
    expect(store.topUpBusy).toBe(false)
    expect(store.topUpError).toBe('')
  })

  it('returns null and keeps the sentence when the backend refuses the amount', async () => {
    createTopUpMock.mockRejectedValue(new Error('refused'))
    const store = useCreditStore()

    const invoiceId = await store.createTopUp(1)

    expect(invoiceId).toBeNull()
    expect(store.topUpError).not.toBe('')
    expect(store.topUpBusy).toBe(false)
  })

  it('clears the previous refusal when it tries again', async () => {
    createTopUpMock.mockRejectedValueOnce(new Error('refused')).mockResolvedValueOnce(9)
    const store = useCreditStore()
    await store.createTopUp(1)

    await store.createTopUp(50)

    expect(store.topUpError).toBe('')
  })
})

describe('useCreditStore.applyToInvoice', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    applyCreditMock.mockReset()
    fetchCreditMock.mockReset()
  })

  it('applies the credit and reads the balance again from the first page', async () => {
    applyCreditMock.mockResolvedValue(undefined)
    fetchCreditMock.mockResolvedValue(credit(20))
    const store = useCreditStore()

    const applied = await store.applyToInvoice('7', 30)

    expect(applied).toBe(true)
    expect(applyCreditMock).toHaveBeenCalledWith('7', 30)
    expect(fetchCreditMock).toHaveBeenCalledWith(1, CREDIT_PAGE_SIZE)
    expect(store.balance).toBe(20)
    expect(store.applyBusy).toBe(false)
  })

  it('returns false, keeps the sentence and does not read the balance when the backend refuses', async () => {
    applyCreditMock.mockRejectedValue(new Error('short'))
    const store = useCreditStore()

    const applied = await store.applyToInvoice(7, 500)

    expect(applied).toBe(false)
    expect(store.applyError).not.toBe('')
    expect(store.applyBusy).toBe(false)
    expect(fetchCreditMock).not.toHaveBeenCalled()
  })
})
