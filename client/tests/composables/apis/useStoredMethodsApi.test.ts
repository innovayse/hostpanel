/**
 * Tests for `composables/apis/useStoredMethodsApi.ts`.
 *
 * Each endpoint function is called for real through `apiFetch` against a stub server, and the
 * test asserts on the path, verb and body that arrived.
 *
 * @module tests/composables/apis/useStoredMethodsApi.test
 */

import { afterEach, describe, expect, it } from 'vitest'
import { expectLastRequest, startTransport } from '../../helpers/stubTransport'
import type { ExpectedRequest } from '../../helpers/stubTransport'
import type { StubServer } from '../../helpers/stubServer'
import { useStoredMethodsApi } from '~/composables/apis/useStoredMethodsApi'
import { ConsentTextVersion } from '~/types/consenttextversion'
import { HttpMethod } from '~/types/httpmethod'

/** The stub started by the current test. */
let stub: StubServer | undefined

afterEach(async () => {
  await stub?.close()
  stub = undefined
})

/** The api surface under test. */
const api = useStoredMethodsApi()

/** Every endpoint function with the request it must send. */
const rows: ExpectedRequest[] = [
  {
    name: 'fetchStoredMethods reads the list',
    call: () => api.fetchStoredMethods(),
    method: HttpMethod.Get,
    url: '/api/portal/client/stored-payment-methods'
  },
  {
    name: 'setDefault posts to the consent default',
    call: () => api.setDefault(5),
    method: HttpMethod.Post,
    url: '/api/portal/client/stored-payment-methods/consents/5/default'
  },
  {
    name: 'beginSave posts module, consent, wording version and return address',
    call: () => api.beginSave('card_gateway', true, ConsentTextVersion.AccountCard, 'https://x/return'),
    method: HttpMethod.Post,
    url: '/api/portal/client/stored-payment-methods/sessions',
    body: { gatewayModuleId: 'card_gateway', consentAccepted: true, consentTextVersion: ConsentTextVersion.AccountCard, returnUrl: 'https://x/return' }
  },
  {
    name: 'completeSave posts the module to the session',
    call: () => api.completeSave('card_gateway', 'sess_1'),
    method: HttpMethod.Post,
    url: '/api/portal/client/stored-payment-methods/sessions/sess_1/complete',
    body: { gatewayModuleId: 'card_gateway' }
  },
  {
    name: 'completeSave encodes the session id into one path segment',
    call: () => api.completeSave('card_gateway', 'a/b'),
    method: HttpMethod.Post,
    url: '/api/portal/client/stored-payment-methods/sessions/a%2Fb/complete',
    body: { gatewayModuleId: 'card_gateway' }
  },
  {
    name: 'revokeConsent deletes by consent id',
    call: () => api.revokeConsent(5),
    method: HttpMethod.Delete,
    url: '/api/portal/client/stored-payment-methods/consents/5'
  }
]

describe('useStoredMethodsApi', () => {
  it.each(rows)('$name', async (row) => {
    stub = await startTransport()

    await row.call()

    expectLastRequest(stub, row)
  })

  it('returns the list the endpoint answers with, and an empty list for a non-array answer', async () => {
    const method = { consentId: 5, gatewayModuleId: 'card_gateway', detailsAvailable: false, brand: null, last4: null, expiryMonth: 0, expiryYear: 0, isDefault: true }
    stub = await startTransport(() => ({ body: [method] }))
    expect(await api.fetchStoredMethods()).toEqual([method])
    await stub.close()

    stub = await startTransport(() => ({ body: {} }))
    expect(await api.fetchStoredMethods()).toEqual([])
  })

  it('returns the session the endpoint answers with', async () => {
    stub = await startTransport(() => ({ body: { sessionId: 's1', mode: 'Redirect', clientToken: null, redirectUrl: 'https://pay.example/s1' } }))

    const session = await api.beginSave('card_gateway', true, ConsentTextVersion.CheckoutAgreement, 'https://x/return')

    expect(session).toEqual({ sessionId: 's1', mode: 'Redirect', clientToken: null, redirectUrl: 'https://pay.example/s1' })
  })
})
