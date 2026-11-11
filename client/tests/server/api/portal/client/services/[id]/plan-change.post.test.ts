/**
 * Tests for `POST /api/portal/client/services/:id/plan-change`: it forwards the body to the signed-in `me/services/{id}/plan-change` endpoint and writes no sentence of its own on a refusal.
 *
 * @module tests/server/api/portal/client/services/[id]/plan-change.post.test
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
  route = await import('~/server/api/portal/client/services/[id]/plan-change.post')
})

afterEach(async () => {
  await backend.close()
})

describe('service plan change route', () => {
  it('posts the body to the service plan change endpoint', async () => {
    await route.default(makeEvent({ params: { id: '5' }, body: { toProductId: 9 }, cookies: await signedIn('jwt') }))

    expect(backend.requests[0]?.method).toBe('POST')
    expect(backend.requests[0]?.url).toBe('/api/me/services/5/plan-change')
    expect(JSON.parse(backend.requests[0]?.body ?? '{}')).toEqual({ toProductId: 9 })
  })

  it('writes no sentence of its own when the backend refuses the change', async () => {
    backend.respondWith(() => ({ status: 409, body: { type: 'SERVICE_CHANGE_ALREADY_PENDING', detail: 'Already open.' } }))

    const error = await refusal(async () => route.default(makeEvent({ params: { id: '5' }, body: {}, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 409, statusMessage: 'Already open.', data: { code: 'SERVICE_CHANGE_ALREADY_PENDING' } })
  })
})
