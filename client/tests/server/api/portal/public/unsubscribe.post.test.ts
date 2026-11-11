/**
 * Tests for `POST /api/portal/public/unsubscribe`: it sends only the trimmed token to the backend
 * without a bearer token, forwards the reader language and passes the answer or refusal through.
 *
 * @module tests/server/api/portal/public/unsubscribe.post.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, refusal, signedIn, startBackend } from '../../../../helpers/bffHarness'
import type { RouteModule } from '../../../../helpers/bffHarness'
import type { StubServer } from '../../../../helpers/stubServer'

/** The stub playing the C# API. */
let backend: StubServer

/** The route under test. */
let route: RouteModule

beforeEach(async () => {
  backend = await startBackend()
  route = await import('~/server/api/portal/public/unsubscribe.post')
})

afterEach(async () => {
  await backend.close()
})

describe('POST /api/portal/public/unsubscribe', () => {
  it('posts only the trimmed token, anonymously, and forwards the reader language', async () => {
    await route.default(makeEvent({ body: { token: ' abc ', other: 1 }, headers: { 'x-language': 'hy' }, cookies: await signedIn('jwt') }))

    expect(backend.requests[0]?.method).toBe('POST')
    expect(backend.requests[0]?.url).toBe('/api/public/unsubscribe')
    expect(backend.requests[0]?.headers['authorization']).toBeUndefined()
    expect(backend.requests[0]?.headers['x-language']).toBe('hy')
    expect(JSON.parse(backend.requests[0]?.body ?? '{}')).toEqual({ token: 'abc' })
  })

  it('sends an empty token when the body carries no string', async () => {
    await route.default(makeEvent({ body: { token: 5 } }))

    expect(JSON.parse(backend.requests[0]?.body ?? '{}')).toEqual({ token: '' })
  })

  it('passes the backend refusal through with its code and sentence', async () => {
    backend.respondWith(() => ({ status: 400, body: { type: 'UNSUBSCRIBE_TOKEN_INVALID', detail: 'This link is not valid.' } }))

    const error = await refusal(() => route.default(makeEvent({ body: { token: 'x' } })))

    expect(error).toMatchObject({ statusCode: 400, statusMessage: 'This link is not valid.', data: { code: 'UNSUBSCRIBE_TOKEN_INVALID' } })
  })
})
