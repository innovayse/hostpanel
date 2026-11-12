/**
 * Tests for `POST /api/portal/order/:id/gateway-payment/start`: it calls the backend `POST /orders/7/gateway-payment/start` with the session bearer, forwards the reader language and returns the backend's answer untouched.
 *
 * @module tests/server/api/portal/order/[id]/gateway-payment/start.post.test
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
  route = await import('~/server/api/portal/order/[id]/gateway-payment/start.post')
})

afterEach(async () => {
  await backend.close()
})

describe('POST /api/portal/order/:id/gateway-payment/start', () => {
  it('calls the backend endpoint and returns its answer', async () => {
    backend.respondWith(() => ({ body: { ok: true } }))

    const result = await route.default(makeEvent({ params: { id: '7' }, body: { value: 'x' }, cookies: await signedIn('jwt') }))

    expect(result).toEqual({ ok: true })
    expect(backend.requests[0]?.method).toBe('POST')
    expect(backend.requests[0]?.url).toBe('/api/orders/7/gateway-payment/start')
    expect(backend.requests[0]?.headers['authorization']).toBe('Bearer jwt')
    expect(JSON.parse(backend.requests[0]?.body ?? '{}')).toEqual({ value: 'x' })
  })

  it('forwards the save choice and the consent version untouched', async () => {
    const body = { module: 'card_gateway', returnUrl: 'https://x/return', paymentToken: 'tok', savePaymentMethod: true, consentTextVersion: 'checkout-agreement-2026-11-b' }

    await route.default(makeEvent({ params: { id: '7' }, body, cookies: await signedIn('jwt') }))

    expect(JSON.parse(backend.requests[0]?.body ?? '{}')).toEqual(body)
  })

  it('forwards the reader language to the backend', async () => {
    const event = makeEvent({ params: { id: '7' }, body: { value: 'x' }, headers: { 'x-language': 'hy' }, cookies: await signedIn('jwt') })

    await route.default(event)

    expect(backend.requests[0]?.headers['x-language']).toBe('hy')
  })

  it('passes the backend refusal through with its code and sentence', async () => {
    backend.respondWith(() => ({ status: 409, body: { type: 'SOME_REFUSAL', detail: 'Refused by the backend.' } }))

    const error = await refusal(async () => route.default(makeEvent({ params: { id: '7' }, body: { value: 'x' }, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 409, statusMessage: 'Refused by the backend.', data: { code: 'SOME_REFUSAL' } })
  })
})
