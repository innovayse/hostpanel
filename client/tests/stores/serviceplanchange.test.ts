/**
 * Unit tests for the plan change store, through the real API composable and transport against a
 * stub server: loading the options, submitting a request and what a refusal leaves behind.
 *
 * @module tests/stores/serviceplanchange.test
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { startTransport } from '../helpers/stubTransport'
import type { StubServer } from '../helpers/stubServer'
import { useServicePlanChangeStore } from '~/stores/serviceplanchange'
import { ActionOutcome } from '~/types/actionoutcome'
import { ServiceChangeStatus } from '~/types/servicechangestatus'
import { ServiceChangeType } from '~/types/servicechangetype'

/** The stub playing the BFF. */
let stub: StubServer

/** One plan the service can move to. */
const option = { productId: 3, name: 'Pro', type: ServiceChangeType.Upgrade, newRecurringAmount: 20, prorationAmount: 5, currencyCode: 'USD' }

describe('useServicePlanChangeStore', () => {
  beforeEach(async () => {
    setActivePinia(createPinia())
    stub = await startTransport()
  })

  afterEach(async () => {
    await stub.close()
  })

  it('loads the options of the service', async () => {
    stub.respondWith(() => ({ body: [option] }))
    const store = useServicePlanChangeStore()

    await store.load('4')

    expect(stub.requests).toHaveLength(1)
    expect(stub.requests[0]).toMatchObject({ method: 'GET', url: '/api/portal/client/services/4/plan-change-options' })
    expect(store.options).toEqual([option])
    expect(store.loading).toBe(false)
  })

  it('keeps the sentence when loading fails and shows no options', async () => {
    stub.respondWith(() => ({ status: 404, body: { detail: 'No such service.' } }))
    const store = useServicePlanChangeStore()
    store.options = [option]

    await store.load('4')

    expect(store.options).toEqual([])
    expect(store.loadError).toBe('No such service.')
  })

  it('keeps the result of an accepted request', async () => {
    const accepted = { requestId: 1, type: ServiceChangeType.Upgrade, status: ServiceChangeStatus.Approved, prorationAmount: 5, currencyCode: 'USD', invoiceId: 8 }
    stub.respondWith(() => ({ body: accepted }))
    const store = useServicePlanChangeStore()

    const outcome = await store.submit('4', 3)

    expect(outcome).toBe(ActionOutcome.Succeeded)
    expect(stub.requests[0]).toMatchObject({ method: 'POST', url: '/api/portal/client/services/4/plan-change' })
    expect(JSON.parse(stub.requests[0]?.body ?? '')).toEqual({ toProductId: 3 })
    expect(store.result).toEqual(accepted)
    expect(store.submitting).toBe(false)
  })

  it('reports Failed and keeps the sentence when the request is refused', async () => {
    stub.respondWith(() => ({ status: 400, body: { detail: 'Not allowed.' } }))
    const store = useServicePlanChangeStore()

    const outcome = await store.submit('4', 3)

    expect(outcome).toBe(ActionOutcome.Failed)
    expect(store.submitError).toBe('Not allowed.')
    expect(store.result).toBeNull()
  })

  it('resets to the empty state', async () => {
    stub.respondWith(() => ({ status: 400, body: { detail: 'Not allowed.' } }))
    const store = useServicePlanChangeStore()
    await store.submit('4', 3)

    store.reset()

    expect(store.submitError).toBe('')
    expect(store.options).toEqual([])
    expect(store.result).toBeNull()
  })
})
