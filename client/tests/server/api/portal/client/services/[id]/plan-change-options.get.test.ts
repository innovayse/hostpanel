/**
 * Tests for `GET /api/portal/client/services/:id/plan-change-options`: it reads the signed-in `me/services/{id}/plan-change-options` endpoint and writes no sentence of its own on a refusal.
 *
 * @module tests/server/api/portal/client/services/[id]/plan-change-options.get.test
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
  route = await import('~/server/api/portal/client/services/[id]/plan-change-options.get')
})

afterEach(async () => {
  await backend.close()
})

describe('service plan change options route', () => {
  it('reads the backend endpoint and returns its answer', async () => {
    backend.respondWith(() => ({ body: [{ productId: 3 }] }))

    const result = await route.default(makeEvent({ params: { id: '7' }, cookies: await signedIn('jwt') }))

    expect(result).toEqual([{ productId: 3 }])
    expect(backend.requests[0]?.method).toBe('GET')
    expect(backend.requests[0]?.url).toBe('/api/me/services/7/plan-change-options')
    expect(backend.requests[0]?.headers['authorization']).toBe('Bearer jwt')
  })

  it('passes the backend refusal through with its code and sentence', async () => {
    backend.respondWith(() => ({ status: 400, body: { type: 'SERVICE_NOT_ACTIVE', detail: 'Not active.' } }))

    const error = await refusal(async () => route.default(makeEvent({ params: { id: '7' }, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 400, statusMessage: 'Not active.', data: { code: 'SERVICE_NOT_ACTIVE' } })
  })
})
