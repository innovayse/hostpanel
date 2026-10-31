/**
 * Tests for `POST /api/portal/client/payment-methods/confirm-setup`: it calls the backend `POST /me/payment-methods/confirm-setup` with the session bearer, forwards the reader language and returns the backend's answer untouched.
 *
 * @module tests/server/api/portal/client/payment-methods/confirm-setup.post.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, refusal, startBackend, signedIn } from '../../../../../helpers/bffHarness'
import type { RouteModule } from '../../../../../helpers/bffHarness'
import type { StubServer } from '../../../../../helpers/stubServer'

/** The stub playing the C# API. */
let backend: StubServer

/** The route under test. */
let route: RouteModule

beforeEach(async () => {
  backend = await startBackend()
  route = await import('~/server/api/portal/client/payment-methods/confirm-setup.post')
})

afterEach(async () => {
  await backend.close()
})

describe('POST /api/portal/client/payment-methods/confirm-setup', () => {
  it('calls the backend endpoint and returns its answer', async () => {
    backend.respondWith(() => ({ body: { ok: true } }))

    const result = await route.default(makeEvent({ body: { setupIntentId: 'seti_1' }, cookies: await signedIn('jwt') }))

    expect(result).toEqual({ ok: true })
    expect(backend.requests[0]?.method).toBe('POST')
    expect(backend.requests[0]?.url).toBe('/api/me/payment-methods/confirm-setup')
    expect(backend.requests[0]?.headers['authorization']).toBe('Bearer jwt')
    expect(JSON.parse(backend.requests[0]?.body ?? '{}')).toEqual({ setupIntentId: 'seti_1' })
  })

  it('forwards the reader language to the backend', async () => {
    await route.default(makeEvent({ body: { setupIntentId: 'seti_1' }, headers: { 'x-language': 'hy' }, cookies: await signedIn('jwt') }))

    expect(backend.requests[0]?.headers['x-language']).toBe('hy')
  })

  it('passes the backend refusal through with its code and sentence', async () => {
    backend.respondWith(() => ({ status: 409, body: { type: 'SOME_REFUSAL', detail: 'Refused by the backend.' } }))

    const error = await refusal(async () => route.default(makeEvent({ body: { setupIntentId: 'seti_1' }, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 409, statusMessage: 'Refused by the backend.', data: { code: 'SOME_REFUSAL' } })
  })
})
