/**
 * Tests for `usePayerAsyncData`: the async-data key carries the payer's currency, so a server render
 * quoted in one currency is never reused by a browser whose payer currency is another (#662).
 *
 * The real currency and client stores run: the payer's currency is set the way the app sets it, on
 * the signed-in account. Only Nuxt's `useAsyncData` is a spy.
 *
 * @module tests/composables/usePayerAsyncData.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { usePayerAsyncData } from '~/composables/usePayerAsyncData'
import { useClientStore } from '~/stores/client'
import { makeClientUser } from '~/tests/helpers/clientUser'

/** Spy standing in for Nuxt's `useAsyncData`. */
const useAsyncData = vi.fn((..._args: unknown[]) => ({ pending: false }))

/**
 * Reads the key getter the composable handed to `useAsyncData`.
 *
 * @returns The key getter of the last call.
 */
const keyGetter = (): (() => string) => useAsyncData.mock.calls.at(-1)?.[0] as () => string

/**
 * Signs in an account billed in a currency, or nobody when the code is null.
 *
 * @param code - The account's currency code, or null for no signed-in account.
 */
const payWith = (code: string | null): void => {
  useClientStore().user = code === null ? null : makeClientUser({ currency: code })
}

describe('usePayerAsyncData', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    payWith('USD')
    useAsyncData.mockClear()
    vi.stubGlobal('useAsyncData', useAsyncData)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('keys the data by the payer currency, so a different currency never reuses the payload', () => {
    usePayerAsyncData('home:tld-pricing', async () => null)

    expect(keyGetter()()).toBe('home:tld-pricing:USD')
    payWith('AMD')
    expect(keyGetter()()).toBe('home:tld-pricing:AMD')
  })

  it('keeps a stable key before the payer currency resolves', () => {
    payWith(null)

    usePayerAsyncData('domains:catalogue', async () => null)

    expect(keyGetter()()).toBe('domains:catalogue:')
  })

  it('passes the handler and options through and returns the async data', () => {
    /**
     * The read the composable is handed and must pass on untouched.
     *
     * @returns A constant, since only the handler's identity is asserted.
     */
    const handler = async (): Promise<number> => 1
    const options = { watch: [] }

    const result = usePayerAsyncData('hosting:plans', handler, options)

    expect(useAsyncData.mock.calls.at(-1)?.[1]).toBe(handler)
    expect(useAsyncData.mock.calls.at(-1)?.[2]).toBe(options)
    expect(result).toEqual({ pending: false })
  })
})
