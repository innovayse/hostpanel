/**
 * Tests for `POST /api/portal/client/credit/top-ups`: it calls the backend `POST /me/credit/top-ups` with the session bearer, sends the
 * amount body, forwards the reader language and returns the backend's answer (the new invoice's id) untouched.
 *
 * @module tests/server/api/portal/client/credit/top-ups.post.test
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
  route = await import('~/server/api/portal/client/credit/top-ups.post')
})

afterEach(async () => {
  await backend.close()
})

describe('POST /api/portal/client/credit/top-ups', () => {
  it('calls the backend endpoint with the session bearer and returns the new invoice id', async () => {
    backend.respondWith(() => ({ status: 201, body: 88 }))

    const result = await route.default(makeEvent({ body: { amount: 40 }, cookies: await signedIn('jwt') }))

    expect(result).toBe(88)
    expect(backend.requests[0]?.method).toBe('POST')
    expect(backend.requests[0]?.url).toBe('/api/me/credit/top-ups')
    expect(backend.requests[0]?.headers['authorization']).toBe('Bearer jwt')
    expect(JSON.parse(backend.requests[0]?.body ?? '{}')).toEqual({ amount: 40 })
  })

  it('forwards the reader language to the backend', async () => {
    const event = makeEvent({ body: { amount: 40 }, headers: { 'x-language': 'hy' }, cookies: await signedIn('jwt') })

    await route.default(event)

    expect(backend.requests[0]?.headers['x-language']).toBe('hy')
  })

  it('passes the backend refusal through with its code and sentence', async () => {
    backend.respondWith(() => ({ status: 400, body: { type: 'CREDIT_TOP_UP_AMOUNT_OUT_OF_RANGE', detail: 'The amount is outside the range.' } }))

    const error = await refusal(async () => route.default(makeEvent({ body: { amount: 1 }, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 400, statusMessage: 'The amount is outside the range.', data: { code: 'CREDIT_TOP_UP_AMOUNT_OUT_OF_RANGE' } })
  })
})
