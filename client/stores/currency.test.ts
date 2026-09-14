/**
 * Tests for `stores/currency.ts`'s `payerCode` precedence.
 *
 * The rule mirrors the backend's `PayerCurrencyResolver.ForCallerAsync`: a signed-in client's
 * own recorded currency wins outright, a guest's chosen currency is used only while it is
 * still a valid loaded code, and the base currency is the final fallback. Getting the order
 * wrong here is how a stale guest selection overrides a signed-in client's fixed currency, or
 * an invalid leftover selection silently wins over the base.
 *
 * @module stores/currency.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

/** The mocked signed-in client, reset before each test. */
let mockUser: { currency?: string | null } | null = null

vi.mock('~/stores/client', () => ({
  /**
   * Stands in for the real client store so `payerCode` can be exercised without the rest of
   * the client-area state.
   *
   * @returns An object exposing only the `user` field {@link payerCode} reads.
   */
  useClientStore: () => ({ user: mockUser }),
}))

vi.mock('~/composables/apis/useCurrencyApi', () => ({
  useCurrencyApi: () => ({ loadBillingCurrencies: vi.fn() }),
}))

const { useCurrencyStore } = await import('~/stores/currency')

const CURRENCIES = [
  { code: 'AMD', numeric: '051', prefix: '', suffix: ' ֏', decimals: 0, isBase: true },
  { code: 'USD', numeric: '840', prefix: '$', suffix: '', decimals: 2, isBase: false },
]

describe('useCurrencyStore.payerCode', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    mockUser = null
  })

  it('falls back to the base currency when nothing else is known', () => {
    const store = useCurrencyStore()
    store.currencies = CURRENCIES

    expect(store.payerCode).toBe('AMD')
  })

  it('uses the guest\'s chosen currency when it is a valid loaded code', () => {
    const store = useCurrencyStore()
    store.currencies = CURRENCIES
    store.selectedCode = 'USD'

    expect(store.payerCode).toBe('USD')
  })

  it('ignores a guest selection that is not (or no longer) a loaded currency', () => {
    const store = useCurrencyStore()
    store.currencies = CURRENCIES
    store.selectedCode = 'EUR'

    expect(store.payerCode).toBe('AMD')
  })

  it('prefers the signed-in client\'s own currency over any guest selection', () => {
    mockUser = { currency: 'USD' }
    const store = useCurrencyStore()
    store.currencies = CURRENCIES
    store.selectedCode = 'AMD'

    expect(store.payerCode).toBe('USD')
  })

  it('falls through to the guest selection when the signed-in client has no currency set', () => {
    mockUser = { currency: null }
    const store = useCurrencyStore()
    store.currencies = CURRENCIES
    store.selectedCode = 'USD'

    expect(store.payerCode).toBe('USD')
  })
})
