/**
 * Tests for `stores/unsubscribe.ts`, through the real API composable and transport against a stub
 * server: an accepted link ends Done, a refused one ends Failed with the API's sentence, and the
 * token goes to the API as it is, empty included.
 *
 * @module tests/stores/unsubscribe.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { startTransport } from '../helpers/stubTransport'
import type { StubServer } from '../helpers/stubServer'
import { useUnsubscribeStore } from '~/stores/unsubscribe'
import { UnsubscribeStatus } from '~/types/unsubscribestatus'

/** The path the unsubscribe call goes to. */
const UNSUBSCRIBE_PATH = '/api/portal/public/unsubscribe'

/** The stub playing the BFF. */
let stub: StubServer

/**
 * The tokens the stub received, oldest first.
 *
 * @returns The `token` of each request body.
 */
const sentTokens = (): unknown[] => stub.requests.map(request => (JSON.parse(request.body) as { token: unknown }).token)

describe('useUnsubscribeStore', () => {
  beforeEach(async () => {
    setActivePinia(createPinia())
    stub = await startTransport(() => ({ status: 204 }))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('starts idle with no sentence', () => {
    const store = useUnsubscribeStore()

    expect(store.status).toBe(UnsubscribeStatus.Idle)
    expect(store.error).toBe('')
  })

  it('reports Done when the API accepts the token', async () => {
    const store = useUnsubscribeStore()

    const status = await store.unsubscribe('tok')

    expect(stub.requests).toHaveLength(1)
    expect(stub.requests[0]).toMatchObject({ method: 'POST', url: UNSUBSCRIBE_PATH })
    expect(sentTokens()).toEqual(['tok'])
    expect(status).toBe(UnsubscribeStatus.Done)
    expect(store.status).toBe(UnsubscribeStatus.Done)
    expect(store.error).toBe('')
  })

  it('keeps the API sentence and reports Failed when the token is refused', async () => {
    stub.respondWith(() => ({ status: 400, body: { detail: 'This link is not valid.' } }))
    const store = useUnsubscribeStore()

    const status = await store.unsubscribe('bad')

    expect(status).toBe(UnsubscribeStatus.Failed)
    expect(store.error).toBe('This link is not valid.')
  })

  it('sends an empty token to the API instead of refusing it itself', async () => {
    stub.respondWith(() => ({ status: 400, body: { detail: 'A token is required.' } }))
    const store = useUnsubscribeStore()

    await store.unsubscribe('')

    expect(sentTokens()).toEqual([''])
    expect(store.error).toBe('A token is required.')
  })

  it('clears an earlier refusal when it is tried again', async () => {
    stub.respondWith(() => ({ status: 400, body: { detail: 'Expired.' } }))
    const store = useUnsubscribeStore()
    await store.unsubscribe('tok')
    expect(store.error).toBe('Expired.')

    stub.respondWith(() => ({ status: 204 }))
    await store.unsubscribe('tok')

    expect(store.status).toBe(UnsubscribeStatus.Done)
    expect(store.error).toBe('')
  })
})
