/**
 * Tests for `GET /api/portal/client/plugins/:plugin/page?path=…`: the page an address names is asked of the
 * backend's page lookup under the reserved `ui` segment, its refusal travels on with its code, and a malformed
 * plugin id or address never reaches the backend.
 *
 * @module tests/server/api/portal/client/plugins/[plugin]/page.get.test
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
  route = await import('~/server/api/portal/client/plugins/[plugin]/page.get')
})

afterEach(async () => {
  await backend.close()
})

describe('plugin page lookup route', () => {
  it('reads the page view the address names', async () => {
    backend.respondWith(() => ({ body: { id: 'site', path: '/sites/{siteId}' } }))

    const result = await route.default(makeEvent({ params: { plugin: 'acme-sites' }, query: { path: '/sites/blog' }, cookies: await signedIn('jwt') }))

    expect(result).toEqual({ id: 'site', path: '/sites/{siteId}' })
    expect(backend.requests[0]?.url).toBe('/api/client/plugins/ui/acme-sites/pages/by-path?path=%2Fsites%2Fblog')
  })

  it('forwards a non-Latin address encoded once', async () => {
    await route.default(makeEvent({ params: { plugin: 'acme-sites' }, query: { path: '/sites/կայք' }, cookies: await signedIn('jwt') }))

    expect(backend.requests[0]?.url).toBe(`/api/client/plugins/ui/acme-sites/pages/by-path?path=${encodeURIComponent('/sites/կայք')}`)
  })

  it('asks for the plugin\'s root page with `/` alone', async () => {
    backend.respondWith(() => ({ body: { id: 'home', path: '/' } }))

    await route.default(makeEvent({ params: { plugin: 'acme-sites' }, query: { path: '/' }, cookies: await signedIn('jwt') }))

    expect(backend.requests[0]?.url).toBe('/api/client/plugins/ui/acme-sites/pages/by-path?path=%2F')
  })

  it('passes the backend\'s refusal on with its code and sentence', async () => {
    backend.respondWith(() => ({ status: 404, body: { type: 'PLUGIN_PAGE_NOT_FOUND', detail: 'No such page.' } }))

    const error = await refusal(async () => route.default(makeEvent({ params: { plugin: 'acme-sites' }, query: { path: '/x' }, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 404, statusMessage: 'No such page.', data: { code: 'PLUGIN_PAGE_NOT_FOUND' } })
  })

  it.each([
    [{ plugin: '../admin' }, { path: '/x' }],
    [{ plugin: 'Acme_Sites' }, { path: '/x' }],
    [{ plugin: 'ui' }, { path: '/x' }],
    [{ plugin: 'slots' }, { path: '/x' }],
    [{ plugin: 'acme-sites' }, { path: '/sites/../admin' }],
    [{ plugin: 'acme-sites' }, {}],
  ])('refuses %o %o before calling the backend', async (params: Record<string, string>, query: Record<string, string>) => {
    const error = await refusal(async () => route.default(makeEvent({ params, query, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 400 })
    expect(backend.requests).toHaveLength(0)
  })
})
