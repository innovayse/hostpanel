/**
 * Unit tests for the plan change store: loading the options, submitting a request and what a
 * refusal leaves behind.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { computed, ref } from 'vue'
import { ActionOutcome } from '~/types/actionoutcome'
import { ServiceChangeStatus } from '~/types/servicechangestatus'
import { ServiceChangeType } from '~/types/servicechangetype'

/** The mocked plan change endpoints, reset before every test. */
const api = vi.hoisted(() => ({
  fetchPlanChangeOptions: vi.fn(),
  requestPlanChange: vi.fn()
}))

vi.stubGlobal('ref', ref)
vi.stubGlobal('computed', computed)

vi.mock('~/composables/apis/useClientApi', () => ({
  /**
   * Stands in for the real API composable so the store runs without transport.
   *
   * @returns The mocked endpoints.
   */
  useClientApi: () => api
}))

const { useServicePlanChangeStore } = await import('~/stores/serviceplanchange')

/** One plan the service can move to. */
const option = { productId: 3, name: 'Pro', type: ServiceChangeType.Upgrade, newRecurringAmount: 20, prorationAmount: 5, currencyCode: 'USD' }

describe('useServicePlanChangeStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    api.fetchPlanChangeOptions.mockReset()
    api.requestPlanChange.mockReset()
  })

  it('loads the options of the service', async () => {
    api.fetchPlanChangeOptions.mockResolvedValue([option])
    const store = useServicePlanChangeStore()

    await store.load('4')

    expect(api.fetchPlanChangeOptions).toHaveBeenCalledWith('4')
    expect(store.options).toEqual([option])
    expect(store.loading).toBe(false)
  })

  it('keeps the sentence when loading fails and shows no options', async () => {
    api.fetchPlanChangeOptions.mockRejectedValue(new Error('no'))
    const store = useServicePlanChangeStore()
    store.options = [option]

    await store.load('4')

    expect(store.options).toEqual([])
    expect(store.loadError).not.toBe('')
  })

  it('keeps the result of an accepted request', async () => {
    const accepted = { requestId: 1, type: ServiceChangeType.Upgrade, status: ServiceChangeStatus.Approved, prorationAmount: 5, currencyCode: 'USD', invoiceId: 8 }
    api.requestPlanChange.mockResolvedValue(accepted)
    const store = useServicePlanChangeStore()

    const outcome = await store.submit('4', 3)

    expect(outcome).toBe(ActionOutcome.Succeeded)
    expect(api.requestPlanChange).toHaveBeenCalledWith('4', 3)
    expect(store.result).toEqual(accepted)
    expect(store.submitting).toBe(false)
  })

  it('reports Failed and keeps the sentence when the request is refused', async () => {
    api.requestPlanChange.mockRejectedValue(new Error('no'))
    const store = useServicePlanChangeStore()

    const outcome = await store.submit('4', 3)

    expect(outcome).toBe(ActionOutcome.Failed)
    expect(store.submitError).not.toBe('')
    expect(store.result).toBeNull()
  })

  it('resets to the empty state', async () => {
    api.requestPlanChange.mockRejectedValue(new Error('no'))
    const store = useServicePlanChangeStore()
    await store.submit('4', 3)

    store.reset()

    expect(store.submitError).toBe('')
    expect(store.options).toEqual([])
    expect(store.result).toBeNull()
  })
})
