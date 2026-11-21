/**
 * Tests for `GET /api/portal/public/legal/:page`: it reads one legal page's rows from the backend's public legal
 * text route, keeps the ones with a value, refuses a malformed page, and answers an empty object when the backend
 * cannot answer.
 *
 * @module tests/server/api/portal/public/legal/[page].get.test
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
  backend = await startBackend()
  route = await import('~/server/api/portal/public/legal/[page].get')
})

afterEach(async () => {
  await backend.close()
})

describe('GET /api/portal/public/legal/:page', () => {
  it('reads the page from the backend and returns its rows with a value by key', async () => {
    backend.respondWith(() => ({
      body: [
        { key: 'portal.legal.acceptable_use', value: 'English.' },
        { key: 'portal.legal.acceptable_use.ru', value: '' },
        { key: 'portal.legal.acceptable_use.hy', value: 'Հայերեն։' },
      ],
    }))

    const result = await route.default(makeEvent({ params: { page: 'acceptable_use' }, headers: { 'x-language': 'ru' } }))

    expect(result).toEqual({ 'portal.legal.acceptable_use': 'English.', 'portal.legal.acceptable_use.hy': 'Հայերեն։' })
    expect(backend.requests[0]?.url).toBe('/api/settings/public/legal/acceptable_use')
    expect(backend.requests[0]?.headers['x-language']).toBe('ru')
  })

  it('refuses a malformed page before calling the backend', async () => {
    const error = await refusal(() => route.default(makeEvent({ params: { page: '../settings' } })))

    expect(error).toMatchObject({ statusCode: 400, data: { code: 'BAD_ROUTE_PARAM' } })
    expect(backend.requests).toHaveLength(0)
  })

  it('answers an empty object when the backend sends nothing', async () => {
    backend.respondWith(() => ({ body: null }))

    expect(await route.default(makeEvent({ params: { page: 'terms' } }))).toEqual({})
  })

  it('answers an empty object when the backend refuses, so the page keeps its built-in text', async () => {
    backend.respondWith(() => ({ status: 404, body: { type: 'SETTING_NOT_FOUND', detail: 'Not found.' } }))

    expect(await route.default(makeEvent({ params: { page: 'terms' } }))).toEqual({})
  })
})
