/**
 * Tests for `POST /api/portal/client/stored-payment-methods/sessions/:sessionId/complete`: it calls the
 * backend `POST /me/stored-payment-methods/sessions/s1/complete` and returns the answer untouched.
 *
 * @module tests/server/api/portal/client/stored-payment-methods/sessions/[sessionId]/complete.post.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, refusal, startBackend, signedIn } from '../../../../../../../helpers/bffHarness'
import type { RouteModule } from '../../../../../../../helpers/bffHarness'
import type { StubServer } from '../../../../../../../helpers/stubServer'

/** The stub playing the C# API. */
let backend: StubServer

/** The route under test. */
let route: RouteModule

beforeEach(async () => {
  backend = await startBackend()
  route = await import('~/server/api/portal/client/stored-payment-methods/sessions/[sessionId]/complete.post')
})

afterEach(async () => {
  await backend.close()
})

describe('POST /api/portal/client/stored-payment-methods/sessions/:sessionId/complete', () => {
  it('calls the backend endpoint with the body and returns its answer', async () => {
    backend.respondWith(() => ({ body: { consentId: 5 } }))

    const result = await route.default(makeEvent({ params: { sessionId: 's1' }, body: { gatewayModuleId: 'card_gateway' }, cookies: await signedIn('jwt') }))

    expect(result).toEqual({ consentId: 5 })
    expect(backend.requests[0]?.method).toBe('POST')
    expect(backend.requests[0]?.url).toBe('/api/me/stored-payment-methods/sessions/s1/complete')
    expect(backend.requests[0]?.headers['authorization']).toBe('Bearer jwt')
    expect(JSON.parse(backend.requests[0]?.body ?? '{}')).toEqual({ gatewayModuleId: 'card_gateway' })
  })

  it('refuses a malformed session id before calling the backend', async () => {
    const error = await refusal(async () => route.default(makeEvent({ params: { sessionId: '../x' }, body: {}, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 400 })
    expect(backend.requests).toHaveLength(0)
  })

  it('passes the backend refusal through with its code and sentence', async () => {
    backend.respondWith(() => ({ status: 404, body: { type: 'CARD_CONSENT_NOT_FOUND', detail: 'Not found.' } }))

    const error = await refusal(async () => route.default(makeEvent({ params: { sessionId: 's1' }, body: {}, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 404, statusMessage: 'Not found.', data: { code: 'CARD_CONSENT_NOT_FOUND' } })
  })
})
