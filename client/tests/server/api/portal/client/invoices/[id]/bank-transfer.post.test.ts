/**
 * Tests for `POST /api/portal/client/invoices/:id/bank-transfer`: it calls the backend `POST /me/invoices/7/bank-transfer` with the session bearer, sends no client id and no body, forwards the reader language and returns the backend's answer untouched.
 *
 * @module tests/server/api/portal/client/invoices/[id]/bank-transfer.post.test
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
  route = await import('~/server/api/portal/client/invoices/[id]/bank-transfer.post')
})

afterEach(async () => {
  await backend.close()
})

describe('POST /api/portal/client/invoices/:id/bank-transfer', () => {
  it('calls the backend endpoint with the session bearer and returns its answer', async () => {
    backend.respondWith(() => ({ body: { reference: 'INV-7' } }))

    const result = await route.default(makeEvent({ params: { id: '7' }, cookies: await signedIn('jwt') }))

    expect(result).toEqual({ reference: 'INV-7' })
    expect(backend.requests[0]?.method).toBe('POST')
    expect(backend.requests[0]?.url).toBe('/api/me/invoices/7/bank-transfer')
    expect(backend.requests[0]?.headers['authorization']).toBe('Bearer jwt')
  })

  it('forwards the reader language to the backend', async () => {
    const event = makeEvent({ params: { id: '7' }, headers: { 'x-language': 'ru' }, cookies: await signedIn('jwt') })

    await route.default(event)

    expect(backend.requests[0]?.headers['x-language']).toBe('ru')
  })

  it('passes the backend refusal through with its code and sentence', async () => {
    backend.respondWith(() => ({ status: 409, body: { type: 'BANK_TRANSFER_NOT_AVAILABLE', detail: 'Bank transfer is not available right now.' } }))

    const error = await refusal(async () => route.default(makeEvent({ params: { id: '7' }, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 409, statusMessage: 'Bank transfer is not available right now.', data: { code: 'BANK_TRANSFER_NOT_AVAILABLE' } })
  })
})
