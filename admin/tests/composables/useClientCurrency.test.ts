/**
 * Tests for useClientCurrency: no currency while no client is chosen, the billing currency of the
 * chosen client once read, a client without one, a refused read, and a pick replaced mid-read.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { ref } from 'vue'
import { useClientCurrency } from '@/composables/useClientCurrency'
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
    stubRoutes({ 'GET /api/clients/7': () => detail('AMD'), 'GET /api/clients/8': () => detail('EUR') })
    /** The picked client id, changed later to follow a new pick. */
    const clientId = ref(7)
    /** The billing currency the composable reads for the picked client. */
    const currency = useClientCurrency(clientId)
    await flushPromises()
    expect(currency.value).toBe('AMD')

    clientId.value = 8
    await flushPromises()
    expect(currency.value).toBe('EUR')
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
