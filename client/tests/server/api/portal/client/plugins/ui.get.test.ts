/**
 * Tests for `GET /api/portal/client/plugins/ui`: a pass-through of the backend's plugin index
 * (`PluginUiDto`), labels already in the reader's language, and its refusals unchanged.
 *
 * @module tests/server/api/portal/client/plugins/ui.get.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, refusal, signedIn, startBackend } from '../../../../../helpers/bffHarness'
import type { RouteModule } from '../../../../../helpers/bffHarness'
import type { StubServer } from '../../../../../helpers/stubServer'

/** The stub playing the C# API. */
let backend: StubServer

/** The route under test. */
let route: RouteModule

/** The index the backend answers: one menu item beside its page, labelled in Armenian. */
const INDEX = {
  menus: [{ pluginId: 'acme-sites', id: 'sites', label: 'Իմ կայքերը', page: 'sites', icon: 'globe', order: 10 }],
  pages: [{ pluginId: 'acme-sites', id: 'sites', path: '/sites', title: 'Իմ կայքերը' }],
}

beforeEach(async () => {
  backend = await startBackend()
  route = await import('~/server/api/portal/client/plugins/ui.get')
})

afterEach(async () => {
  await backend.close()
})

describe('GET /api/portal/client/plugins/ui', () => {
  it('reads the index with the session token and in the reader\'s language, and passes it on unchanged', async () => {
    backend.respondWith(() => ({ body: INDEX }))

    const result = await route.default(makeEvent({ cookies: await signedIn('jwt'), headers: { 'x-language': 'hy' } }))

    expect(result).toEqual(INDEX)
    expect(backend.requests[0]?.url).toBe('/api/client/plugins/ui')
    expect(backend.requests[0]?.headers['authorization']).toBe('Bearer jwt')
    expect(backend.requests[0]?.headers['x-language']).toBe('hy')
  })

  it('passes an empty index on as empty lists', async () => {
    backend.respondWith(() => ({ body: { menus: [], pages: [] } }))

    expect(await route.default(makeEvent({ cookies: await signedIn('jwt') }))).toEqual({ menus: [], pages: [] })
  })

  it('passes the backend\'s refusal on with its code and sentence', async () => {
    backend.respondWith(() => ({ status: 503, headers: { 'retry-after': '5' }, body: { type: 'IDENTITY_PLUGIN_UNAVAILABLE', detail: 'Try again shortly.' } }))
    const event = makeEvent({ cookies: await signedIn('jwt') })

    const error = await refusal(async () => route.default(event))

    expect(error).toMatchObject({ statusCode: 503, statusMessage: 'Try again shortly.', data: { code: 'IDENTITY_PLUGIN_UNAVAILABLE' } })
    expect(event.responseHeaders['Retry-After']).toBe('5')
  })
})
