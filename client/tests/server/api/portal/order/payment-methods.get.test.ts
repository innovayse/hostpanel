/**
 * Tests for `GET /api/portal/order/payment-methods`: it calls the backend `GET /payment-methods` with the session bearer, forwards the reader language and returns the backend's answer untouched.
 *
 * @module tests/server/api/portal/order/payment-methods.get.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, refusal, startBackend, signedIn } from '../../../../helpers/bffHarness'
import type { RouteModule } from '../../../../helpers/bffHarness'
import type { StubServer } from '../../../../helpers/stubServer'

/** The stub playing the C# API. */
let backend: StubServer

/** The route under test. */
let route: RouteModule

beforeEach(async () => {
  backend = await startBackend()
  route = await import('~/server/api/portal/order/payment-methods.get')
})

afterEach(async () => {
  await backend.close()
})

describe('GET /api/portal/order/payment-methods', () => {
  it('calls the backend endpoint and returns its answer', async () => {
    backend.respondWith(() => ({ body: { ok: true } }))

    const result = await route.default(makeEvent({ cookies: await signedIn('jwt') }))

    expect(result).toEqual({ ok: true })
    expect(backend.requests[0]?.method).toBe('GET')
    expect(backend.requests[0]?.url).toBe('/api/payment-methods')
    expect(backend.requests[0]?.headers['authorization']).toBe('Bearer jwt')
  })

  it('forwards the reader language to the backend', async () => {
    const event = makeEvent({ headers: { 'x-language': 'hy' }, cookies: await signedIn('jwt') })

    await route.default(event)

    expect(backend.requests[0]?.headers['x-language']).toBe('hy')
  })

  it('passes the backend refusal through with its code and sentence', async () => {
    backend.respondWith(() => ({ status: 409, body: { type: 'SOME_REFUSAL', detail: 'Refused by the backend.' } }))

    const error = await refusal(async () => route.default(makeEvent({ cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 409, statusMessage: 'Refused by the backend.', data: { code: 'SOME_REFUSAL' } })
  })

  it('forwards the cart\'s products, one backend key each, and the invoice', async () => {
    await route.default(makeEvent({ query: { productIds: '4,9', invoiceId: '12' }, cookies: await signedIn('jwt') }))

    expect(backend.requests[0]?.url).toBe('/api/payment-methods?productIds=4&productIds=9&invoiceId=12')
  })

  it.each([
    [{ productIds: '4,x' }],
    [{ productIds: '4,' }],
    [{ productIds: ['4', '9'] }],
    [{ invoiceId: 'abc' }],
    [{ productIds: Array.from({ length: 101 }, (_, i) => i + 1).join(',') }],
  ])('refuses a query that is not ids (%j) before the backend is called', async (query) => {
    const error = await refusal(async () => route.default(makeEvent({ query, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 400, data: { code: 'BAD_QUERY' } })
    expect(backend.requests).toHaveLength(0)
  })
})
