/**
 * Tests for `stores/unsubscribe.ts`: an accepted link ends Done, a refused one ends Failed with the
 * API's sentence, and the token goes to the API as it is, empty included.
 *
 * @module tests/stores/unsubscribe.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { UnsubscribeStatus } from '~/types/unsubscribestatus'

/** The mocked unsubscribe call, re-created for every test. */
const unsubscribeMock = vi.fn()

vi.mock('~/composables/apis/useUnsubscribeApi', () => ({
  /**
   * Stands in for the real API composable so the store runs without transport.
   *
   * @returns Only the endpoint the store reaches.
   */
  useUnsubscribeApi: () => ({ unsubscribe: unsubscribeMock })
}))

const { useUnsubscribeStore } = await import('~/stores/unsubscribe')

describe('useUnsubscribeStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    unsubscribeMock.mockReset()
  })

  it('starts pending with no sentence', () => {
    const store = useUnsubscribeStore()

    expect(store.status).toBe(UnsubscribeStatus.Pending)
    expect(store.error).toBe('')
  })

  it('reports Done when the API accepts the token', async () => {
    unsubscribeMock.mockResolvedValue('')
    const store = useUnsubscribeStore()

    const status = await store.unsubscribe('tok')

    expect(unsubscribeMock).toHaveBeenCalledWith('tok')
    expect(status).toBe(UnsubscribeStatus.Done)
    expect(store.status).toBe(UnsubscribeStatus.Done)
    expect(store.error).toBe('')
  })

  it('keeps the API sentence and reports Failed when the token is refused', async () => {
    unsubscribeMock.mockRejectedValue({ data: { detail: 'This link is not valid.' } })
    const store = useUnsubscribeStore()

    const status = await store.unsubscribe('bad')

    expect(status).toBe(UnsubscribeStatus.Failed)
    expect(store.error).toBe('This link is not valid.')
  })

  it('sends an empty token to the API instead of refusing it itself', async () => {
    unsubscribeMock.mockRejectedValue({ data: { detail: 'A token is required.' } })
    const store = useUnsubscribeStore()

    await store.unsubscribe('')

    expect(unsubscribeMock).toHaveBeenCalledWith('')
    expect(store.error).toBe('A token is required.')
  })

  it('clears an earlier refusal when it is tried again', async () => {
    unsubscribeMock.mockRejectedValueOnce({ data: { detail: 'Expired.' } }).mockResolvedValueOnce('')
    const store = useUnsubscribeStore()

    await store.unsubscribe('tok')
    await store.unsubscribe('tok')

    expect(store.status).toBe(UnsubscribeStatus.Done)
    expect(store.error).toBe('')
  })
})
