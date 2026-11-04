/**
 * Tests for `GET /api/portal/client/payment-methods/modules`: it calls the backend `GET /me/payment-methods/modules` with the session bearer, forwards the reader language and returns the backend's answer untouched.
 *
 * @module tests/server/api/portal/client/payment-methods/modules.get.test
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
  route = await import('~/server/api/portal/client/payment-methods/modules.get')
})

afterEach(async () => {
  await backend.close()
})

describe('GET /api/portal/client/payment-methods/modules', () => {
  it('calls the backend endpoint and returns its answer', async () => {
    backend.respondWith(() => ({ body: [{ module: 'stripe', displayName: 'Card' }] }))

    const result = await route.default(makeEvent({ cookies: await signedIn('jwt') }))

    expect(result).toEqual([{ module: 'stripe', displayName: 'Card' }])
    expect(backend.requests[0]?.method).toBe('GET')
    expect(backend.requests[0]?.url).toBe('/api/me/payment-methods/modules')
    expect(backend.requests[0]?.headers['authorization']).toBe('Bearer jwt')
  })

  it('forwards the reader language to the backend', async () => {
    await route.default(makeEvent({ headers: { 'x-language': 'hy' }, cookies: await signedIn('jwt') }))

    expect(backend.requests[0]?.headers['x-language']).toBe('hy')
  })

  it('passes the backend refusal through with its code and sentence', async () => {
    backend.respondWith(() => ({ status: 403, body: { type: 'CLIENT_PERMISSION_DENIED', detail: 'Not allowed.' } }))

    const error = await refusal(async () => route.default(makeEvent({ cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 403, statusMessage: 'Not allowed.', data: { code: 'CLIENT_PERMISSION_DENIED' } })
  })
})
