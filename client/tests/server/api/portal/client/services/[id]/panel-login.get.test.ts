/**
 * Tests for `GET /api/portal/client/services/:id/panel-login`: it calls the backend `GET /me/services/7/panel-login` with the
 * session bearer, forwards the reader language and returns the backend's answer untouched.
 *
 * @module tests/server/api/portal/client/services/[id]/panel-login.get.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, refusal, startBackend, signedIn } from '../../../../../../helpers/bffHarness'
import type { RouteModule } from '../../../../../../helpers/bffHarness'
import type { StubServer } from '../../../../../../helpers/stubServer'

/** The stub playing the C# API. */
let backend: StubServer

/** The route under test. */
let route: RouteModule

/** How to sign in by hand, as the backend answers it. */
const details = { username: 'ada', domain: 'a.test', serverAddress: '203.0.113.9', loginUrl: 'https://panel.example:8083/login/' }

beforeEach(async () => {
  backend = await startBackend()
  route = await import('~/server/api/portal/client/services/[id]/panel-login.get')
})

afterEach(async () => {
  await backend.close()
})

describe('GET /api/portal/client/services/:id/panel-login', () => {
  it('calls the backend endpoint and returns its answer', async () => {
    backend.respondWith(() => ({ body: details }))

    const result = await route.default(makeEvent({ params: { id: '7' }, cookies: await signedIn('jwt') }))

    expect(result).toEqual(details)
    expect(backend.requests[0]?.method).toBe('GET')
    expect(backend.requests[0]?.url).toBe('/api/me/services/7/panel-login')
    expect(backend.requests[0]?.headers['authorization']).toBe('Bearer jwt')
  })

  it('forwards the reader language to the backend', async () => {
    backend.respondWith(() => ({ body: details }))
    const event = makeEvent({ params: { id: '7' }, headers: { 'x-language': 'hy' }, cookies: await signedIn('jwt') })

    await route.default(event)

    expect(backend.requests[0]?.headers['x-language']).toBe('hy')
  })

  it('refuses an id that is not a number with 400, and never calls the backend', async () => {
    const error = await refusal(async () => route.default(makeEvent({ params: { id: '7/../../clients' }, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 400, data: { code: 'BAD_ROUTE_PARAM' } })
    expect(backend.requests).toHaveLength(0)
  })

  it('passes the backend refusal through with its code and sentence', async () => {
    backend.respondWith(() => ({ status: 409, body: { type: 'PANEL_OPERATION_NOT_SUPPORTED', detail: 'The panel plugin does not declare the login capability.' } }))

    const error = await refusal(async () => route.default(makeEvent({ params: { id: '7' }, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 409, statusMessage: 'The panel plugin does not declare the login capability.', data: { code: 'PANEL_OPERATION_NOT_SUPPORTED' } })
  })
})
