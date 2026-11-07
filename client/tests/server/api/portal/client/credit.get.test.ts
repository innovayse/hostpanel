/**
 * Tests for `GET /api/portal/client/credit`: it calls the backend `GET /me/credit` with the session bearer, forwards only the page and
 * the page size, sends no client id, forwards the reader language and returns the backend's answer untouched.
 *
 * @module tests/server/api/portal/client/credit.get.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, refusal, startBackend, signedIn } from '../../../../helpers/bffHarness'
import type { RouteModule } from '../../../../helpers/bffHarness'
import type { StubServer } from '../../../../helpers/stubServer'

/** The stub playing the C# API. */
let backend: StubServer

/** The route under test. */
let route: RouteModule

beforeEach(async () => {
  backend = await startBackend()
  route = await import('~/server/api/portal/client/credit.get')
})

afterEach(async () => {
  await backend.close()
})

describe('GET /api/portal/client/credit', () => {
  it('calls the backend endpoint with the session bearer and returns its answer', async () => {
    const credit = { balance: 12.5, currency: 'USD', topUpMinimum: 5, topUpMaximum: 1000, entries: { items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 } }
    backend.respondWith(() => ({ body: credit }))

    const result = await route.default(makeEvent({ cookies: await signedIn('jwt') }))

    expect(result).toEqual(credit)
    expect(backend.requests[0]?.method).toBe('GET')
    expect(backend.requests[0]?.url).toBe('/api/me/credit')
    expect(backend.requests[0]?.headers['authorization']).toBe('Bearer jwt')
  })

  it('forwards the page and the page size, and nothing else the browser put in the query', async () => {
    await route.default(makeEvent({ query: { page: '2', pageSize: '10', clientId: '99' }, cookies: await signedIn('jwt') }))

    expect(backend.requests[0]?.url).toBe('/api/me/credit?page=2&pageSize=10')
  })

  it('refuses a repeated query key rather than guess which value was meant', async () => {
    const error = await refusal(async () => route.default(makeEvent({
      query: { page: ['1', '2'] } as unknown as Record<string, string>,
      cookies: await signedIn('jwt')
    })))

    expect(error).toMatchObject({ statusCode: 400, data: { code: 'BAD_QUERY' } })
    expect(backend.requests).toHaveLength(0)
  })

  it('forwards the reader language to the backend', async () => {
    await route.default(makeEvent({ headers: { 'x-language': 'ru' }, cookies: await signedIn('jwt') }))

    expect(backend.requests[0]?.headers['x-language']).toBe('ru')
  })

  it('passes the backend refusal through with its code and sentence', async () => {
    backend.respondWith(() => ({ status: 404, body: { type: 'CLIENT_PROFILE_NOT_FOUND', detail: 'No client profile.' } }))

    const error = await refusal(async () => route.default(makeEvent({ cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 404, statusMessage: 'No client profile.', data: { code: 'CLIENT_PROFILE_NOT_FOUND' } })
  })
})
