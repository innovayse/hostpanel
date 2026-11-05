/**
 * Tests for `POST /api/portal/client/stored-payment-methods/consents/:id/default`: it calls the backend
 * `POST /me/stored-payment-methods/consents/5/default` with the session bearer.
 *
 * @module tests/server/api/portal/client/stored-payment-methods/consents/[id]/default.post.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, refusal, startBackend, signedIn } from '../../../../../../../helpers/bffHarness'
import type { RouteModule } from '../../../../../../../helpers/bffHarness'
import type { StubServer } from '../../../../../../../helpers/stubServer'

/** The stub playing the C# API. */
let backend: StubServer

/** The route under test. */
let route: RouteModule

beforeEach(async () => {
  backend = await startBackend()
  route = await import('~/server/api/portal/client/stored-payment-methods/consents/[id]/default.post')
})

afterEach(async () => {
  await backend.close()
})

describe('POST /api/portal/client/stored-payment-methods/consents/:id/default', () => {
  it('calls the backend endpoint with the consent id', async () => {
    backend.respondWith(() => ({ body: {} }))

    await route.default(makeEvent({ params: { id: '5' }, cookies: await signedIn('jwt') }))

    expect(backend.requests[0]?.method).toBe('POST')
    expect(backend.requests[0]?.url).toBe('/api/me/stored-payment-methods/consents/5/default')
    expect(backend.requests[0]?.headers['authorization']).toBe('Bearer jwt')
  })

  it('refuses a non-numeric id before calling the backend', async () => {
    const error = await refusal(async () => route.default(makeEvent({ params: { id: 'abc' }, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 400 })
    expect(backend.requests).toHaveLength(0)
  })

  it('passes the backend refusal through with its code and sentence', async () => {
    backend.respondWith(() => ({ status: 409, body: { type: 'STORED_CREDENTIALS_NOT_SUPPORTED', detail: 'Not supported.' } }))

    const error = await refusal(async () => route.default(makeEvent({ params: { id: '5' }, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 409, statusMessage: 'Not supported.', data: { code: 'STORED_CREDENTIALS_NOT_SUPPORTED' } })
  })
})
