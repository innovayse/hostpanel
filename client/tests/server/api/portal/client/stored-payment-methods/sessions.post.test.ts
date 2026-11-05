/**
 * Tests for `POST /api/portal/client/stored-payment-methods/sessions`: it calls the backend
 * `POST /me/stored-payment-methods/sessions` with the session bearer and returns the answer untouched.
 *
 * @module tests/server/api/portal/client/stored-payment-methods/sessions.post.test
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
  route = await import('~/server/api/portal/client/stored-payment-methods/sessions.post')
})

afterEach(async () => {
  await backend.close()
})

describe('POST /api/portal/client/stored-payment-methods/sessions', () => {
  it('calls the backend endpoint with the body and returns its answer', async () => {
    backend.respondWith(() => ({ body: { sessionId: 's1' } }))

    const result = await route.default(makeEvent({ body: { gatewayModuleId: 'card_gateway', consentAccepted: true, consentTextVersion: 'account-card-2026-11', returnUrl: 'https://x/r' }, cookies: await signedIn('jwt') }))

    expect(result).toEqual({ sessionId: 's1' })
    expect(backend.requests[0]?.method).toBe('POST')
    expect(backend.requests[0]?.url).toBe('/api/me/stored-payment-methods/sessions')
    expect(backend.requests[0]?.headers['authorization']).toBe('Bearer jwt')
    expect(JSON.parse(backend.requests[0]?.body ?? '{}')).toEqual({ gatewayModuleId: 'card_gateway', consentAccepted: true, consentTextVersion: 'account-card-2026-11', returnUrl: 'https://x/r' })
  })

  it('passes the backend refusal through with its code and sentence', async () => {
    backend.respondWith(() => ({ status: 409, body: { type: 'STORED_CREDENTIALS_NOT_SUPPORTED', detail: 'Not supported.' } }))

    const error = await refusal(async () => route.default(makeEvent({ body: {}, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 409, statusMessage: 'Not supported.', data: { code: 'STORED_CREDENTIALS_NOT_SUPPORTED' } })
  })
})
