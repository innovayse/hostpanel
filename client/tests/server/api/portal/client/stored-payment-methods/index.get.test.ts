/**
 * Tests for `GET /api/portal/client/stored-payment-methods`: it calls the backend
 * `GET /me/stored-payment-methods` with the session bearer and returns the answer untouched.
 *
 * @module tests/server/api/portal/client/stored-payment-methods/index.get.test
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
  route = await import('~/server/api/portal/client/stored-payment-methods/index.get')
})

afterEach(async () => {
  await backend.close()
})

describe('GET /api/portal/client/stored-payment-methods', () => {
  it('calls the backend endpoint and returns its answer', async () => {
    backend.respondWith(() => ({ body: [{ consentId: 5 }] }))

    const result = await route.default(makeEvent({ cookies: await signedIn('jwt') }))

    expect(result).toEqual([{ consentId: 5 }])
    expect(backend.requests[0]?.method).toBe('GET')
    expect(backend.requests[0]?.url).toBe('/api/me/stored-payment-methods')
    expect(backend.requests[0]?.headers['authorization']).toBe('Bearer jwt')
  })

  it('passes the backend refusal through with its code and sentence', async () => {
    backend.respondWith(() => ({ status: 404, body: { type: 'CLIENT_PROFILE_NOT_FOUND', detail: 'No profile.' } }))

    const error = await refusal(async () => route.default(makeEvent({ cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 404, statusMessage: 'No profile.', data: { code: 'CLIENT_PROFILE_NOT_FOUND' } })
  })
})
