/**
 * Tests for useClientCurrency: no currency while no client is chosen, the billing currency of the
 * chosen client once read, a client without one, a refused read, a pick replaced mid-read whose
 * late answer is dropped, and a return to no client that resets the currency.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { ref } from 'vue'
import { useClientCurrency } from '@/composables/useClientCurrency'
import { CurrencyCode } from '@/types/currency'
import { json, problem } from '../helpers/fetch'
import { stubRoutes } from '../helpers/routes'

/**
 * Builds the client detail answer.
 *
 * @param currency - The billing currency, null for none.
 * @returns The response.
 */
const detail = (currency: string | null) => json({ id: 7, preferences: { currency } })

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useClientCurrency', () => {
  it('is empty and reads nothing while no client is chosen', async () => {
    setActivePinia(createPinia())
    const mock = stubRoutes({})
    const currency = useClientCurrency(ref(0))
    await flushPromises()

    expect(currency.value).toBeNull()
    expect(mock).not.toHaveBeenCalled()
  })

  it('reads the billing currency of the chosen client and follows a new pick', async () => {
    setActivePinia(createPinia())
    stubRoutes({ 'GET /api/clients/7': () => detail(CurrencyCode.Amd), 'GET /api/clients/8': () => detail(CurrencyCode.Eur) })
    /** The picked client id, changed later to follow a new pick. */
    const clientId = ref(7)
    /** The billing currency the composable reads for the picked client. */
    const currency = useClientCurrency(clientId)
    await flushPromises()
    expect(currency.value).toBe(CurrencyCode.Amd)

    clientId.value = 8
    await flushPromises()
    expect(currency.value).toBe(CurrencyCode.Eur)
  })

  it('keeps the currency of the latest pick when an earlier read resolves last', async () => {
    setActivePinia(createPinia())
    /** Resolves the answer of client 7 once the test lets it. */
    let release: (response: Response) => void = () => {}
    /** The answer of client 7, held back. */
    const slow = new Promise<Response>((resolve) => {
      release = resolve
    })
    vi.stubGlobal(
      'fetch',
      vi.fn((url: string) => (url.includes('/clients/7') ? slow : Promise.resolve(detail(CurrencyCode.Eur)))),
    )
    /** The picked client id, switched while client 7 is still being read. */
    const clientId = ref(7)
    /** The billing currency the composable reads for the picked client. */
    const currency = useClientCurrency(clientId)
    clientId.value = 8
    await flushPromises()
    expect(currency.value).toBe(CurrencyCode.Eur)

    release(detail(CurrencyCode.Amd))
    await flushPromises()
    expect(currency.value).toBe(CurrencyCode.Eur)
  })

  it('resets to null when the pick goes back to no client', async () => {
    setActivePinia(createPinia())
    stubRoutes({ 'GET /api/clients/7': () => detail(CurrencyCode.Amd) })
    /** The picked client id, cleared later. */
    const clientId = ref(7)
    /** The billing currency the composable reads for the picked client. */
    const currency = useClientCurrency(clientId)
    await flushPromises()
    expect(currency.value).toBe(CurrencyCode.Amd)

    clientId.value = 0
    await flushPromises()
    expect(currency.value).toBeNull()
  })

  it('is empty for a client without a billing currency', async () => {
    setActivePinia(createPinia())
    stubRoutes({ 'GET /api/clients/7': () => detail(null) })
    const currency = useClientCurrency(ref(7))
    await flushPromises()

    expect(currency.value).toBeNull()
  })

  it('is empty when the read is refused', async () => {
    setActivePinia(createPinia())
    stubRoutes({ 'GET /api/clients/7': () => problem('No access.', 403) })
    const currency = useClientCurrency(ref(7))
    await flushPromises()

    expect(currency.value).toBeNull()
  })
})
