/**
 * Tests for `POST /api/portal/client/payment-methods/sessions`: it calls the backend `POST /me/payment-methods/sessions` with the session bearer and a return address built from the portal's own origin, never from the body, and returns the backend's answer untouched.
 *
 * @module tests/server/api/portal/client/payment-methods/sessions.post.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, refusal, startBackend, signedIn } from '../../../../../helpers/bffHarness'
import type { RouteModule } from '../../../../../helpers/bffHarness'
import type { StubServer } from '../../../../../helpers/stubServer'
import { BAD_BODY } from '~/server/utils/readBodyObject'

/** The stub playing the C# API. */
let backend: StubServer

/** The route under test. */
let route: RouteModule

beforeEach(async () => {
  backend = await startBackend()
  route = await import('~/server/api/portal/client/payment-methods/sessions.post')
})

afterEach(async () => {
  await backend.close()
})

describe('POST /api/portal/client/payment-methods/sessions', () => {
  it('calls the backend endpoint with the module, the agreement and the portal return address', async () => {
    backend.respondWith(() => ({ body: { sessionId: 's1' } }))

    const result = await route.default(makeEvent({
      body: { gatewayModuleId: 'stripe', consentAccepted: true },
      headers: { host: 'portal.example.test' },
      cookies: await signedIn('jwt'),
    }))

    expect(result).toEqual({ sessionId: 's1' })
    expect(backend.requests[0]?.method).toBe('POST')
    expect(backend.requests[0]?.url).toBe('/api/me/payment-methods/sessions')
    expect(backend.requests[0]?.headers['authorization']).toBe('Bearer jwt')
    expect(JSON.parse(backend.requests[0]?.body ?? '{}')).toEqual({
      gatewayModuleId: 'stripe',
      consentAccepted: true,
      returnUrl: 'http://portal.example.test/client/account?tab=payment&module=stripe',
    })
  })

  it('never takes the return address from the body', async () => {
    await route.default(makeEvent({
      body: { gatewayModuleId: 'stripe', consentAccepted: true, returnUrl: 'https://evil.example/steal' },
      headers: { host: 'portal.example.test' },
      cookies: await signedIn('jwt'),
    }))

    expect(JSON.parse(backend.requests[0]?.body ?? '{}')['returnUrl']).toBe('http://portal.example.test/client/account?tab=payment&module=stripe')
  })

  it('treats anything but true as no agreement', async () => {
    await route.default(makeEvent({ body: { gatewayModuleId: 'stripe', consentAccepted: 'yes' }, cookies: await signedIn('jwt') }))

    expect(JSON.parse(backend.requests[0]?.body ?? '{}')['consentAccepted']).toBe(false)
  })

  it('forwards the reader language to the backend', async () => {
    await route.default(makeEvent({ body: { gatewayModuleId: 'stripe', consentAccepted: true }, headers: { 'x-language': 'hy' }, cookies: await signedIn('jwt') }))

    expect(backend.requests[0]?.headers['x-language']).toBe('hy')
  })

  it.each([
    ['no module', {}],
    ['a module that is not a string', { gatewayModuleId: 7 }],
  ])('refuses %s with a coded 400 and sends nothing', async (_name, body) => {
    const error = await refusal(async () => route.default(makeEvent({ body, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 400, data: { code: BAD_BODY } })
    expect(backend.requests).toHaveLength(0)
  })

  it('passes the backend refusal through with its code and sentence', async () => {
    backend.respondWith(() => ({ status: 400, body: { type: 'VALIDATION_ERROR', detail: 'Agree first.' } }))

    const error = await refusal(async () => route.default(makeEvent({ body: { gatewayModuleId: 'stripe', consentAccepted: false }, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 400, statusMessage: 'Agree first.', data: { code: 'VALIDATION_ERROR' } })
  })
})
