/**
 * Tests for `GET /api/portal/public/products/domain`: it forwards to the backend's route for the
 * product a domain line hangs on, with the reader's language, and passes the answer or the refusal
 * through untouched.
 *
 * @module tests/server/api/portal/public/products/domain.get.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, refusal, startBackend } from '../../../../../helpers/bffHarness'
import type { RouteModule } from '../../../../../helpers/bffHarness'
import type { StubServer } from '../../../../../helpers/stubServer'

/** The stub playing the C# API. */
let backend: StubServer

/** The route under test. */
let route: RouteModule

beforeEach(async () => {
  backend = await startBackend(() => ({ body: { id: 9, name: 'Domain Registration' } }))
  route = await import('~/server/api/portal/public/products/domain.get')
})

afterEach(async () => {
  await backend.close()
})

describe('GET /api/portal/public/products/domain', () => {
  it('asks the backend for the domain product, forwarding the reader language, and returns it as sent', async () => {
    const result = await route.default(makeEvent({ headers: { 'x-language': 'ru' } }))

    expect(backend.requests[0]?.url).toBe('/api/public/products/domain')
    expect(backend.requests[0]?.headers['x-language']).toBe('ru')
    expect(result).toEqual({ id: 9, name: 'Domain Registration' })
  })

  it('passes the refusal for an operator with no domain product through with its code and sentence', async () => {
    backend.respondWith(() => ({ status: 503, body: { type: 'DOMAIN_PRODUCT_NOT_CONFIGURED', detail: 'Domains cannot be ordered here.' } }))

    const error = await refusal(() => route.default(makeEvent()))

    expect(error).toMatchObject({ statusCode: 503, statusMessage: 'Domains cannot be ordered here.', data: { code: 'DOMAIN_PRODUCT_NOT_CONFIGURED' } })
  })
})
