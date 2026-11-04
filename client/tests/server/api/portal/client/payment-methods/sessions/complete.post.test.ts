/**
 * Tests for `POST /api/portal/client/payment-methods/sessions/complete`: it calls the backend `POST /me/payment-methods/sessions/complete` with the session bearer, answers 400 for a module or session that is not a string, and returns the backend's answer untouched.
 *
 * @module tests/server/api/portal/client/payment-methods/sessions/complete.post.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, refusal, startBackend, signedIn } from '../../../../../../helpers/bffHarness'
import type { RouteModule } from '../../../../../../helpers/bffHarness'
import type { StubServer } from '../../../../../../helpers/stubServer'
import { BAD_BODY } from '~/server/utils/readBodyObject'

/** The stub playing the C# API. */
let backend: StubServer

/** The route under test. */
let route: RouteModule

beforeEach(async () => {
  backend = await startBackend()
  route = await import('~/server/api/portal/client/payment-methods/sessions/complete.post')
})

afterEach(async () => {
  await backend.close()
})

describe('POST /api/portal/client/payment-methods/sessions/complete', () => {
  it('calls the backend endpoint and returns its answer', async () => {
    backend.respondWith(() => ({ body: { ok: true } }))

    const result = await route.default(makeEvent({ body: { gatewayModuleId: 'stripe', sessionId: 'seti_1', extra: 'x' }, cookies: await signedIn('jwt') }))

    expect(result).toEqual({ ok: true })
    expect(backend.requests[0]?.method).toBe('POST')
    expect(backend.requests[0]?.url).toBe('/api/me/payment-methods/sessions/complete')
    expect(backend.requests[0]?.headers['authorization']).toBe('Bearer jwt')
    expect(JSON.parse(backend.requests[0]?.body ?? '{}')).toEqual({ gatewayModuleId: 'stripe', sessionId: 'seti_1' })
  })

  it('forwards the reader language to the backend', async () => {
    await route.default(makeEvent({ body: { gatewayModuleId: 'stripe', sessionId: 'seti_1' }, headers: { 'x-language': 'hy' }, cookies: await signedIn('jwt') }))

    expect(backend.requests[0]?.headers['x-language']).toBe('hy')
  })

  it.each([
    ['no body', undefined],
    ['no session', { gatewayModuleId: 'stripe' }],
    ['a session that is not a string', { gatewayModuleId: 'stripe', sessionId: { id: 1 } }],
    ['a module that is not a string', { gatewayModuleId: 5, sessionId: 'seti_1' }],
  ])('refuses %s with a coded 400 and sends nothing', async (_name, body) => {
    const error = await refusal(async () => route.default(makeEvent({ body, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 400, data: { code: BAD_BODY } })
    expect(backend.requests).toHaveLength(0)
  })

  it('passes the backend refusal through with its code and sentence', async () => {
    backend.respondWith(() => ({ status: 409, body: { type: 'CARD_SETUP_NOT_COMPLETED', detail: 'Not finished.' } }))

    const error = await refusal(async () => route.default(makeEvent({ body: { gatewayModuleId: 'stripe', sessionId: 'seti_1' }, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 409, statusMessage: 'Not finished.', data: { code: 'CARD_SETUP_NOT_COMPLETED' } })
  })
})
