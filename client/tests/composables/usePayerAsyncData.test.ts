/**
 * Tests for `usePayerAsyncData`: the async-data key carries the payer's currency, so a server render
 * quoted in one currency is never reused by a browser whose payer currency is another (#662).
 *
 * @module tests/composables/usePayerAsyncData.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reactive } from 'vue'
import { usePayerAsyncData } from '~/composables/usePayerAsyncData'

/** The fake currency store; `payerCode` is mutated by each test. */
const currency = reactive<{ payerCode: string | null }>({ payerCode: null })

vi.mock('~/stores/currency', () => ({ useCurrencyStore: () => currency }))

/** Spy standing in for Nuxt's `useAsyncData`. */
const useAsyncData = vi.fn((..._args: unknown[]) => ({ pending: false }))

/**
 * Reads the key getter the composable handed to `useAsyncData`.
 *
 * @returns The key getter of the last call.
 */
const keyGetter = (): (() => string) => useAsyncData.mock.calls.at(-1)?.[0] as () => string

describe('usePayerAsyncData', () => {
  beforeEach(() => {
    currency.payerCode = 'USD'
    useAsyncData.mockClear()
    vi.stubGlobal('useAsyncData', useAsyncData)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('keys the data by the payer currency, so a different currency never reuses the payload', () => {
    usePayerAsyncData('home:tld-pricing', async () => null)

    expect(keyGetter()()).toBe('home:tld-pricing:USD')
    currency.payerCode = 'AMD'
    expect(keyGetter()()).toBe('home:tld-pricing:AMD')
  })

  it('keeps a stable key before the payer currency resolves', () => {
    currency.payerCode = null

    usePayerAsyncData('domains:catalogue', async () => null)

    expect(keyGetter()()).toBe('domains:catalogue:')
  })

  it('passes the handler and options through and returns the async data', () => {
    const handler = async (): Promise<number> => 1
    const options = { watch: [] }

    const result = usePayerAsyncData('hosting:plans', handler, options)

    expect(useAsyncData.mock.calls.at(-1)?.[1]).toBe(handler)
    expect(useAsyncData.mock.calls.at(-1)?.[2]).toBe(options)
    expect(result).toEqual({ pending: false })
  })
})
