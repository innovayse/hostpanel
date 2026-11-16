/**
 * Tests for the clients store VAT number check: the check is POSTed and the open client is read again so the status shown is
 * the one the API holds; a refusal keeps the API's sentence, reads nothing and leaves the profile save state alone.
 */

import { describe, expect, it } from 'vitest'
import { useClientsStore } from '@/stores/clients'
import { json, problem, stubFetch, callOf } from '../helpers/fetch'
import { installStoreHooks } from '../helpers/storeHooks'

describe('clientsStore.checkVat', () => {
  installStoreHooks()

  it('POSTs the check, reads the client again and leaves the error empty', async () => {
    const mock = stubFetch(
      new Response(null, { status: 204 }),
      json({ id: 7, vat: { status: 'Valid', checkedAt: '2026-11-10T09:30:00Z', name: 'Acme GmbH', address: null } }),
    )
    const store = useClientsStore()

    expect(await store.checkVat(7)).toBe(true)

    expect(callOf(mock, 0)).toMatchObject({ url: '/api/clients/7/vat-check', method: 'POST' })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/clients/7', method: 'GET' })
    expect(store.current?.vat?.status).toBe('Valid')
    expect(store.vatChecking).toBe(false)
    expect(store.vatError).toBeNull()
  })

  it('keeps the API sentence when the service gave no verdict and reads nothing', async () => {
    const mock = stubFetch(problem('The VAT number service is not answering right now.', 503))
    const store = useClientsStore()

    expect(await store.checkVat(7)).toBe(false)

    expect(store.vatError).toBe('The VAT number service is not answering right now.')
    expect(store.vatChecking).toBe(false)
    expect(mock).toHaveBeenCalledTimes(1)
  })

  it('keeps the sentence of a client with no number', async () => {
    stubFetch(problem('This client has no VAT number to check.', 400))
    const store = useClientsStore()

    expect(await store.checkVat(7)).toBe(false)
    expect(store.vatError).toBe('This client has no VAT number to check.')
  })

  it('does not touch the profile save state', async () => {
    stubFetch(problem('Refused.', 503))
    const store = useClientsStore()

    await store.checkVat(7)

    expect(store.saveError).toBeNull()
    expect(store.externalAccountError).toBeNull()
  })

  it('forgets a refusal when another client is opened', async () => {
    stubFetch(problem('Refused.', 503), json({ id: 8 }))
    const store = useClientsStore()

    await store.checkVat(7)
    expect(store.vatError).toBe('Refused.')

    await store.fetchById(8)

    expect(store.vatError).toBeNull()
  })
})
