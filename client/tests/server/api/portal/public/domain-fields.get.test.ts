/**
 * Tests for `GET /api/portal/public/domain-fields`: it forwards only the three query members to the backend and passes the
 * answer or the refusal through, in the reader's language.
 *
 * @module tests/server/api/portal/public/domain-fields.get.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, refusal, startBackend } from '../../../../helpers/bffHarness'
import type { RouteModule } from '../../../../helpers/bffHarness'
import type { StubServer } from '../../../../helpers/stubServer'

/** The stub playing the C# API. */
let backend: StubServer

/** The route under test. */
let route: RouteModule

beforeEach(async () => {
  backend = await startBackend()
  route = await import('~/server/api/portal/public/domain-fields.get')
})

afterEach(async () => {
  await backend.close()
})

describe('GET /api/portal/public/domain-fields', () => {
  it('forwards domain, operation and holder, and returns the fields', async () => {
    backend.respondWith(() => ({ body: [{ key: 'holder-number' }] }))

    const result = await route.default(makeEvent({
      query: { domain: 'name.alpha', operation: 'register', holder: 'individual', other: 'dropped' },
      headers: { 'x-language': 'ru' },
    }))

    expect(result).toEqual([{ key: 'holder-number' }])
    expect(backend.requests[0]?.method).toBe('GET')
    expect(backend.requests[0]?.url).toBe('/api/domains/fields?domain=name.alpha&operation=register&holder=individual')
    expect(backend.requests[0]?.headers['x-language']).toBe('ru')
  })

  it('passes the backend refusal through with its code and sentence', async () => {
    backend.respondWith(() => ({ status: 400, body: { type: 'VALIDATION_ERROR', detail: 'Not a domain.' } }))

    const error = await refusal(() => route.default(makeEvent({ query: { domain: 'x', operation: 'register' } })))

    expect(error).toMatchObject({ statusCode: 400, statusMessage: 'Not a domain.', data: { code: 'VALIDATION_ERROR' } })
  })
})
