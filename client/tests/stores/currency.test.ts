/**
 * Tests for `stores/currency.ts`'s `payerCode` precedence.
 *
 * The rule mirrors the backend's `PayerCurrencyResolver.ForCallerAsync`: a signed-in client's
 * own recorded currency wins outright, a guest's chosen currency is used only while it is
 * still a valid loaded code, and the base currency is the final fallback. Getting the order
 * wrong here is how a stale guest selection overrides a signed-in client's fixed currency, or
 * an invalid leftover selection silently wins over the base.
 *
 * The real client store holds the signed-in client; no endpoint is reached, so no transport is needed.
 *
 * @module tests/stores/currency.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { makeClientUser } from '../helpers/clientUser'
import { useClientStore } from '~/stores/client'
import { useCurrencyStore } from '~/stores/currency'

/** The currency catalogue the store holds. */
const CURRENCIES = [
  { code: 'AMD', numeric: '051', prefix: '', suffix: ' ֏', decimals: 0, isBase: true },
  { code: 'USD', numeric: '840', prefix: '$', suffix: '', decimals: 2, isBase: false },
]

/**
 * Signs a client in on the real client store, with the given currency on record.
 *
 * @param currency - The currency recorded for the client, or null for none.
 */
const signIn = (currency: string | null): void => {
  useClientStore().user = makeClientUser({ currency })
}

describe('useCurrencyStore.payerCode', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
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
    signIn('USD')
    const store = useCurrencyStore()
    store.currencies = CURRENCIES
    store.selectedCode = 'AMD'

    expect(store.payerCode).toBe('USD')
  })

  it('falls through to the guest selection when the signed-in client has no currency set', () => {
    signIn(null)
    const store = useCurrencyStore()
    store.currencies = CURRENCIES
    store.selectedCode = 'USD'

    expect(store.payerCode).toBe('USD')
  })
})


describe('useCurrencyStore.isFixed and select', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  it('is not fixed for a guest', () => {
    expect(useCurrencyStore().isFixed).toBe(false)
  })

  it('is not fixed for a signed-in client with no currency on record', () => {
    signIn(null)

    expect(useCurrencyStore().isFixed).toBe(false)
  })

  it('is fixed once the signed-in client has a currency on record', () => {
    signIn('AMD')

    expect(useCurrencyStore().isFixed).toBe(true)
  })

  it('stores and persists a guest selection', () => {
    const store = useCurrencyStore()
    store.currencies = CURRENCIES
    store.select('USD')

    expect(store.selectedCode).toBe('USD')
    expect(store.payerCode).toBe('USD')
    expect(localStorage.getItem('innovayse_currency')).toBe('USD')
  })

  it('restores the persisted selection on init', () => {
    localStorage.setItem('innovayse_currency', 'USD')
    const store = useCurrencyStore()
    store.init()

    expect(store.selectedCode).toBe('USD')
  })

  it('waits for the app to be ready before restoring while it hydrates', () => {
    localStorage.setItem('innovayse_currency', 'USD')
    let ready: () => void = () => undefined
    vi.stubGlobal('useNuxtApp', () => ({ isHydrating: true }))
    vi.stubGlobal('onNuxtReady', (callback: () => void) => {
      ready = callback
    })
    const store = useCurrencyStore()
    store.init()

    expect(store.selectedCode).toBeNull()
    ready()
    expect(store.selectedCode).toBe('USD')
    vi.unstubAllGlobals()
  })

  it('ignores a selection for a signed-in client whose currency is fixed', () => {
    signIn('AMD')
    const store = useCurrencyStore()
    store.currencies = CURRENCIES
    store.select('USD')

    expect(store.selectedCode).toBeNull()
    expect(store.payerCode).toBe('AMD')
    expect(localStorage.getItem('innovayse_currency')).toBeNull()
  })
})
