/**
 * Tests for `GET /api/portal/client/plugins/:plugin/icon`: it calls the backend `GET /me/plugins/{id}/icon` with the
 * session bearer, hands the image bytes back under the type the backend declared with the headers that keep an image
 * from being run, and never lets a malformed plugin id reach the backend.
 *
 * @module tests/server/api/portal/client/plugins/[plugin]/icon.get.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, refusal, signedIn, startBackend } from '../../../../../../helpers/bffHarness'
import type { RouteModule } from '../../../../../../helpers/bffHarness'
import type { StubServer } from '../../../../../../helpers/stubServer'

/** The stub playing the C# API. */
let backend: StubServer

/** The route under test. */
let route: RouteModule

/** What the stub answers with: a small SVG as the backend sends it. */
const iconReply = () => ({
  body: '<svg xmlns="http://www.w3.org/2000/svg"/>',
  headers: { 'content-type': 'image/svg+xml', 'cache-control': 'private, max-age=3600' },
})

beforeEach(async () => {
  backend = await startBackend()
  route = await import('~/server/api/portal/client/plugins/[plugin]/icon.get')
})

afterEach(async () => {
  await backend.close()
})

describe('GET /api/portal/client/plugins/:plugin/icon', () => {
  it('asks the backend for the plugin icon with the bearer token', async () => {
    backend.respondWith(iconReply)

    await route.default(makeEvent({ params: { plugin: 'acme-panel' }, cookies: await signedIn('jwt') }))

    expect(backend.requests[0]?.method).toBe('GET')
    expect(backend.requests[0]?.url).toBe('/api/me/plugins/acme-panel/icon')
    expect(backend.requests[0]?.headers['authorization']).toBe('Bearer jwt')
  })

  it('returns the bytes under the declared type, with nosniff and a policy that allows no script', async () => {
    backend.respondWith(iconReply)
    const event = makeEvent({ params: { plugin: 'acme-panel' }, cookies: await signedIn('jwt') })

    const result = await route.default(event)

    expect((result as Buffer).toString('utf8')).toBe('<svg xmlns="http://www.w3.org/2000/svg"/>')
    expect(event.responseHeaders).toMatchObject({
      'content-type': 'image/svg+xml',
      'cache-control': 'private, max-age=3600',
      'x-content-type-options': 'nosniff',
      'content-security-policy': "default-src 'none'; style-src 'unsafe-inline'",
    })
  })

  it('revalidates privately when the backend names no caching rule', async () => {
    backend.respondWith(() => ({ body: '<svg/>', headers: { 'content-type': 'image/svg+xml' } }))
    const event = makeEvent({ params: { plugin: 'acme-panel' }, cookies: await signedIn('jwt') })

    await route.default(event)

    expect(event.responseHeaders['cache-control']).toBe('private, no-cache')
  })

  it('refuses an id that is not a plugin id with 400, and never calls the backend', async () => {
    backend.respondWith(iconReply)

    const error = await refusal(async () => route.default(makeEvent({ params: { plugin: '../services' }, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 400, data: { code: 'BAD_ROUTE_PARAM' } })
    expect(backend.requests).toHaveLength(0)
  })

  it('passes a 404 refusal through with its code and sentence', async () => {
    backend.respondWith(() => ({ status: 404, body: { type: 'PLUGIN_ICON_NOT_FOUND', detail: 'The plugin has no icon.' } }))

    const error = await refusal(async () => route.default(makeEvent({ params: { plugin: 'acme-panel' }, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 404, statusMessage: 'The plugin has no icon.', data: { code: 'PLUGIN_ICON_NOT_FOUND' } })
  })
})
