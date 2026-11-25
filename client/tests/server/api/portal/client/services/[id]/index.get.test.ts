/**
 * Tests for `GET /api/portal/client/services/:id`: it finds one service in the client's list and
 * answers it as the backend sent it, answering 404 with a code when it is not theirs.
 *
 * @module tests/server/api/portal/client/services/[id]/index.get.test
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
  route = await import('~/server/api/portal/client/services/[id]/index.get')
})

afterEach(async () => {
  await backend.close()
})

describe('GET /api/portal/client/services/:id', () => {
  it('answers the matching service unchanged', async () => {
    const match = { id: 7, productId: 2, productName: 'Hosting M', billingCycle: 'Annual', status: 'Active', nextRenewalAt: null, domain: null, username: null, price: 5, firstPaymentAmount: 5, paymentMethod: 'card', server: { name: 'srv', hostname: 'srv.test', ipAddress: '1.2.3.4', panelPlugin: null }, createdAt: '2026-01-05T09:00:00Z' }
    backend.respondWith(() => ({ body: [{ id: 3, productId: 1, productName: 'Other' }, match] }))

    const result = await route.default(makeEvent({ params: { id: '7' }, cookies: await signedIn('jwt'), headers: { 'x-language': 'hy' } }))

    expect(backend.requests[0]?.url).toBe('/api/me/services')
    expect(backend.requests[0]?.headers['authorization']).toBe('Bearer jwt')
    expect(backend.requests[0]?.headers['x-language']).toBe('hy')
    expect(result).toEqual(match)
  })

  it('refuses with 404 and the not-found code when the service is not in the list', async () => {
    backend.respondWith(() => ({ body: [{ id: 3 }] }))

    const error = await refusal(async () => route.default(makeEvent({ params: { id: '7' }, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 404, data: { code: 'SERVICE_NOT_FOUND' } })
  })

  it('passes the backend refusal through with its code and sentence', async () => {
    backend.respondWith(() => ({ status: 500, body: { type: 'INTERNAL_ERROR', detail: 'Failed.' } }))

    const error = await refusal(async () => route.default(makeEvent({ params: { id: '7' }, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 500, statusMessage: 'Failed.', data: { code: 'INTERNAL_ERROR' } })
  })
})
