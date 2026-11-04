/**
 * Tests for `POST /api/portal/order/create`: checkout placing an order, as a signed-in client or
 * as a guest.
 *
 * @module tests/server/api/portal/order/create.post.test
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
  route = await import('~/server/api/portal/order/create.post')
})

afterEach(async () => {
  await backend.close()
})

describe('POST /api/portal/order/create', () => {
  it('forwards the cart body to the orders endpoint and returns the backend answer untouched', async () => {
    backend.respondWith(() => ({ body: { orderId: 7, invoiceId: 11, paymentToken: 'tok' } }))
    const body = { items: [{ productId: 3, billingCycle: 'Monthly' }], paymentMethod: 'card_gateway' }

    const result = await route.default(makeEvent({ body, cookies: await signedIn('jwt'), headers: { 'x-language': 'hy' } }))

    expect(result).toEqual({ orderId: 7, invoiceId: 11, paymentToken: 'tok' })
    expect(backend.requests[0]?.method).toBe('POST')
    expect(backend.requests[0]?.url).toBe('/api/orders')
    expect(backend.requests[0]?.headers['authorization']).toBe('Bearer jwt')
    expect(backend.requests[0]?.headers['x-language']).toBe('hy')
    expect(JSON.parse(backend.requests[0]?.body ?? '{}')).toEqual(body)
  })

  it('sends no bearer for a guest checkout', async () => {
    await route.default(makeEvent({ body: { items: [] } }))

    expect(backend.requests[0]?.headers['authorization']).toBeUndefined()
  })

  it('rethrows a validation refusal with the backend sentence and field errors', async () => {
    backend.respondWith(() => ({
      status: 400,
      body: { type: 'VALIDATION_ERROR', title: 'Invalid', detail: 'Check the fields.', errors: { email: ['Required.'] } },
    }))

    const error = await refusal(() => route.default(makeEvent({ body: {} })))

    expect(error).toMatchObject({
      statusCode: 400,
      statusMessage: 'Check the fields.',
      data: { code: 'VALIDATION_ERROR', errors: { email: ['Required.'] } },
    })
  })
})
