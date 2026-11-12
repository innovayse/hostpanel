/**
 * Tests for `stores/storedmethods.ts`, through the real API composable and transport against a stub
 * server: the sentences, flags and list the stored-method card renders from.
 *
 * @module tests/stores/storedmethods.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { startTransport } from '../helpers/stubTransport'
import { requestsTo, routeTable } from '../helpers/stubRoutes'
import type { StubRouteTable } from '../helpers/stubRoutes'
import type { StubRequest, StubServer } from '../helpers/stubServer'
import { useStoredMethodsStore } from '~/stores/storedmethods'
import { ActionOutcome } from '~/types/actionoutcome'
import { ConsentTextVersion } from '~/types/consenttextversion'
import { SaveMethodMode } from '~/types/savemethodmode'
import type { StoredMethod } from '~/types/storedmethod'
import { readPendingStoredSession, writePendingStoredSession } from '~/utils/pendingStoredSession'

/** The BFF path of the stored-method list. */
const BASE = '/api/portal/client/stored-payment-methods'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test, by `METHOD /path`; the list answers empty unless a test says otherwise. */
let table: StubRouteTable

/** One stored card. */
const stored: StoredMethod = {
  consentId: 5, gatewayModuleId: 'card_gateway', detailsAvailable: true, brand: 'visa', last4: '4242',
  expiryMonth: 9, expiryYear: 2029, isDefault: false
}

/** A refusal as the BFF answers it. */
const refusal = { status: 400, body: { detail: 'Refused.' } }

/**
 * The list reads the stub received.
 *
 * @returns The requests to the list path, oldest first.
 */
const listReads = (): StubRequest[] => requestsTo(stub.requests, BASE)

beforeEach(async () => {
  setActivePinia(createPinia())
  table = { [`GET ${BASE}`]: { body: [] } }
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  window.sessionStorage.clear()
  await stub.close()
})

describe('stored methods store', () => {
  it('loads the list', async () => {
    table[`GET ${BASE}`] = { body: [stored] }
    const store = useStoredMethodsStore()

    await store.load()

    expect(store.methods).toEqual([stored])
    expect(store.loadError).toBe('')
    expect(store.loading).toBe(false)
  })

  it('keeps the current list and the sentence when the read fails', async () => {
    table[`GET ${BASE}`] = { body: [stored] }
    const store = useStoredMethodsStore()
    await store.load()

    table[`GET ${BASE}`] = refusal
    await store.load()

    expect(store.loadError).toBe('Refused.')
    expect(store.methods).toEqual([stored])
  })

  it('opens a redirect session, remembers it and returns the address', async () => {
    table[`POST ${BASE}/sessions`] = { body: { sessionId: 's1', mode: SaveMethodMode.Redirect, clientToken: null, redirectUrl: 'https://pay.example/s1' } }
    const store = useStoredMethodsStore()

    const url = await store.beginSave('card_gateway', true, ConsentTextVersion.AccountCard, 'https://x/return')

    expect(url).toBe('https://pay.example/s1')
    const [sent] = requestsTo(stub.requests, `${BASE}/sessions`)
    expect(sent?.method).toBe('POST')
    expect(JSON.parse(sent?.body ?? '')).toEqual({
      gatewayModuleId: 'card_gateway',
      consentAccepted: true,
      consentTextVersion: ConsentTextVersion.AccountCard,
      returnUrl: 'https://x/return'
    })
    expect(readPendingStoredSession()).toEqual({ gatewayModuleId: 'card_gateway', sessionId: 's1' })
    expect(store.starting).toBe(false)
  })

  it('refuses an embedded session, which this portal cannot show', async () => {
    table[`POST ${BASE}/sessions`] = { body: { sessionId: 's1', mode: SaveMethodMode.EmbeddedToken, clientToken: 'tok', redirectUrl: null } }
    const store = useStoredMethodsStore()

    expect(await store.beginSave('card_gateway', true, ConsentTextVersion.AccountCard, 'https://x/return')).toBeNull()
    expect(store.error).not.toBe('')
    expect(readPendingStoredSession()).toBeNull()
  })

  it('keeps the API sentence when opening the session is refused', async () => {
    table[`POST ${BASE}/sessions`] = refusal
    const store = useStoredMethodsStore()

    expect(await store.beginSave('card_gateway', false, ConsentTextVersion.AccountCard, 'https://x/return')).toBeNull()
    expect(store.error).toBe('Refused.')
    expect(store.starting).toBe(false)
  })

  it('completes nothing when no session is pending', async () => {
    const store = useStoredMethodsStore()

    expect(await store.completePending()).toBeNull()
    expect(stub.requests).toHaveLength(0)
  })

  it('completes the pending session, re-reads the list and forgets the session', async () => {
    writePendingStoredSession({ gatewayModuleId: 'card_gateway', sessionId: 's1' })
    table[`POST ${BASE}/sessions/s1/complete`] = { body: stored }
    table[`GET ${BASE}`] = { body: [stored] }
    const store = useStoredMethodsStore()

    expect(await store.completePending()).toBe(ActionOutcome.Succeeded)
    const [sent] = requestsTo(stub.requests, `${BASE}/sessions/s1/complete`)
    expect(sent?.method).toBe('POST')
    expect(JSON.parse(sent?.body ?? '')).toEqual({ gatewayModuleId: 'card_gateway' })
    expect(store.methods).toEqual([stored])
    expect(readPendingStoredSession()).toBeNull()
  })

  it('forgets the session when completing is refused, as a refused session cannot succeed on a retry', async () => {
    writePendingStoredSession({ gatewayModuleId: 'card_gateway', sessionId: 's1' })
    table[`POST ${BASE}/sessions/s1/complete`] = refusal
    const store = useStoredMethodsStore()

    expect(await store.completePending()).toBe(ActionOutcome.Failed)
    expect(store.error).toBe('Refused.')
    expect(readPendingStoredSession()).toBeNull()
    expect(store.completing).toBe(false)
  })

  it('re-reads the list after a revoke', async () => {
    table[`DELETE ${BASE}/consents/5`] = { status: 204 }
    const store = useStoredMethodsStore()

    expect(await store.revoke(5)).toBe(ActionOutcome.Succeeded)
    expect(requestsTo(stub.requests, `${BASE}/consents/5`).map(request => request.method)).toEqual(['DELETE'])
    expect(listReads()).toHaveLength(1)
    expect(store.revokingId).toBeNull()
  })

  it('keeps the sentence and does not re-read when the revoke is refused', async () => {
    table[`DELETE ${BASE}/consents/5`] = refusal
    const store = useStoredMethodsStore()

    expect(await store.revoke(5)).toBe(ActionOutcome.Failed)
    expect(store.error).toBe('Refused.')
    expect(listReads()).toHaveLength(0)
  })

  it('makes a method the default and re-reads the list', async () => {
    table[`POST ${BASE}/consents/5/default`] = { status: 204 }
    table[`GET ${BASE}`] = { body: [{ ...stored, isDefault: true }] }
    const store = useStoredMethodsStore()

    expect(await store.makeDefault(5)).toBe(ActionOutcome.Succeeded)
    expect(requestsTo(stub.requests, `${BASE}/consents/5/default`).map(request => request.method)).toEqual(['POST'])
    expect(store.methods[0]?.isDefault).toBe(true)
    expect(store.settingDefaultId).toBeNull()
  })

  it('keeps the sentence when the default change is refused', async () => {
    table[`POST ${BASE}/consents/5/default`] = refusal
    const store = useStoredMethodsStore()

    expect(await store.makeDefault(5)).toBe(ActionOutcome.Failed)
    expect(store.error).toBe('Refused.')
    expect(store.settingDefaultId).toBeNull()
  })
})
