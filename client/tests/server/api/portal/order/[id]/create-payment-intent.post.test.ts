/**
 * Tests for `POST /api/portal/order/:id/create-payment-intent`: it calls the backend `POST /orders/7/create-payment-intent` with the session bearer, forwards the reader language and returns the backend's answer untouched.
 *
 * @module tests/server/api/portal/order/[id]/create-payment-intent.post.test
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
  route = await import('~/server/api/portal/order/[id]/create-payment-intent.post')
})

afterEach(async () => {
  await backend.close()
})

describe('POST /api/portal/order/:id/create-payment-intent', () => {
  it('calls the backend endpoint and returns its answer', async () => {
    backend.respondWith(() => ({ body: { ok: true } }))

    const result = await route.default(makeEvent({ params: { id: '7' }, cookies: await signedIn('jwt') }))

    expect(result).toEqual({ ok: true })
    expect(backend.requests[0]?.method).toBe('POST')
    expect(backend.requests[0]?.url).toBe('/api/orders/7/create-payment-intent')
    expect(backend.requests[0]?.headers['authorization']).toBe('Bearer jwt')
  })

  it('forwards the payment token and the choice to save the card, and nothing else from the body', async () => {
    const event = makeEvent({
      params: { id: '7' },
      body: { paymentToken: 'tok', saveCard: true, origin: { ipAddress: '9.9.9.9' } },
      cookies: await signedIn('jwt')
    })

    await route.default(event)

    expect(JSON.parse(backend.requests[0]?.body ?? '{}')).toEqual({ paymentToken: 'tok', saveCard: true })
  })

  it('does not save the card unless the body says so exactly', async () => {
    await route.default(makeEvent({ params: { id: '7' }, body: { paymentToken: 'tok', saveCard: 'yes' }, cookies: await signedIn('jwt') }))

    expect(JSON.parse(backend.requests[0]?.body ?? '{}')).toEqual({ paymentToken: 'tok', saveCard: false })
  })

  it('forwards the reader language to the backend', async () => {
    const event = makeEvent({ params: { id: '7' }, headers: { 'x-language': 'hy' }, cookies: await signedIn('jwt') })

    await route.default(event)

    expect(backend.requests[0]?.headers['x-language']).toBe('hy')
  })

  it('passes the backend refusal through with its code and sentence', async () => {
    backend.respondWith(() => ({ status: 409, body: { type: 'SOME_REFUSAL', detail: 'Refused by the backend.' } }))

    const error = await refusal(async () => route.default(makeEvent({ params: { id: '7' }, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 409, statusMessage: 'Refused by the backend.', data: { code: 'SOME_REFUSAL' } })
  })
})
