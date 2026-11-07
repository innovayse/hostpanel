/**
 * Tests for `GET /api/portal/client/credit-notes`: it calls the backend `GET /me/credit-notes` with the session bearer, forwards the
 * reader language and returns the backend's answer untouched; it sends no client id, because the backend takes the client from the
 * session.
 *
 * @module tests/server/api/portal/client/credit-notes/index.get.test
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
  route = await import('~/server/api/portal/client/credit-notes/index.get')
})

afterEach(async () => {
  await backend.close()
})

describe('GET /api/portal/client/credit-notes', () => {
  it('calls the backend endpoint with the session bearer and returns its answer', async () => {
    backend.respondWith(() => ({ body: [{ id: 3, number: 'CN-0003' }] }))

    const result = await route.default(makeEvent({ cookies: await signedIn('jwt') }))

    expect(result).toEqual([{ id: 3, number: 'CN-0003' }])
    expect(backend.requests[0]?.method).toBe('GET')
    expect(backend.requests[0]?.url).toBe('/api/me/credit-notes')
    expect(backend.requests[0]?.headers['authorization']).toBe('Bearer jwt')
  })

  it('does not forward a client id from the request query', async () => {
    await route.default(makeEvent({ query: { clientId: '99' }, cookies: await signedIn('jwt') }))

    expect(backend.requests[0]?.url).toBe('/api/me/credit-notes')
  })

  it('forwards the reader language to the backend', async () => {
    const event = makeEvent({ headers: { 'x-language': 'hy' }, cookies: await signedIn('jwt') })

    await route.default(event)

    expect(backend.requests[0]?.headers['x-language']).toBe('hy')
  })

  it('passes the backend refusal through with its code and sentence', async () => {
    backend.respondWith(() => ({ status: 404, body: { type: 'CLIENT_PROFILE_NOT_FOUND', detail: 'No client profile.' } }))

    const error = await refusal(async () => route.default(makeEvent({ cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 404, statusMessage: 'No client profile.', data: { code: 'CLIENT_PROFILE_NOT_FOUND' } })
  })
})
