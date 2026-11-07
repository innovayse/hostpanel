/**
 * Tests for `DELETE /api/portal/client/stored-payment-methods/consents/:id`: it calls the backend
 * `DELETE /me/stored-payment-methods/consents/5` with the session bearer.
 *
 * @module tests/server/api/portal/client/stored-payment-methods/consents/[id].delete.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, refusal, startBackend, signedIn } from '../../../../../../helpers/bffHarness'
import type { RouteModule } from '../../../../../../helpers/bffHarness'
import type { StubServer } from '../../../../../../helpers/stubServer'

/** The stub playing the C# API. */
let backend: StubServer

/** The route under test. */
let route: RouteModule

beforeEach(async () => {
  backend = await startBackend()
  route = await import('~/server/api/portal/client/stored-payment-methods/consents/[id].delete')
})

afterEach(async () => {
  await backend.close()
})

describe('DELETE /api/portal/client/stored-payment-methods/consents/:id', () => {
  it('calls the backend endpoint with the consent id', async () => {
    backend.respondWith(() => ({ body: {} }))

    await route.default(makeEvent({ params: { id: '5' }, cookies: await signedIn('jwt') }))

    expect(backend.requests[0]?.method).toBe('DELETE')
    expect(backend.requests[0]?.url).toBe('/api/me/stored-payment-methods/consents/5')
    expect(backend.requests[0]?.headers['authorization']).toBe('Bearer jwt')
  })

  it('refuses a non-numeric id before calling the backend', async () => {
    const error = await refusal(async () => route.default(makeEvent({ params: { id: 'abc' }, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 400 })
    expect(backend.requests).toHaveLength(0)
  })

  it('passes the backend refusal through with its code and sentence', async () => {
    backend.respondWith(() => ({ status: 404, body: { type: 'CARD_CONSENT_NOT_FOUND', detail: 'Not found.' } }))

    const error = await refusal(async () => route.default(makeEvent({ params: { id: '5' }, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 404, statusMessage: 'Not found.', data: { code: 'CARD_CONSENT_NOT_FOUND' } })
  })
})
