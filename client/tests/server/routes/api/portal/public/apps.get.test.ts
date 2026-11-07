/**
 * Tests for `GET /api/portal/public/apps`: the launcher app list is read from the shell, and an
 * unreachable shell leaves an empty launcher rather than a failed header.
 *
 * @module tests/server/routes/api/portal/public/apps.get.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, refusal, setRuntimeConfig, startBackend } from '../../../../../helpers/bffHarness'
import type { RouteModule } from '../../../../../helpers/bffHarness'
import type { StubServer } from '../../../../../helpers/stubServer'

/** The stub standing in for the platform shell. */
let shell: StubServer

/** The route under test. */
let route: RouteModule

beforeEach(async () => {
  shell = await startBackend()
  setRuntimeConfig({ public: { authMode: 'sso', mainUrl: shell.baseUrl } })
  route = await import('~/server/routes/api/portal/public/apps.get')
})

afterEach(async () => {
  await shell.close()
})

describe('GET /api/portal/public/apps', () => {
  it('returns the list the shell owns', async () => {
    shell.respondWith(() => ({ body: [{ id: 'home', name: 'Home', url: 'https://shell.example.test' }] }))

    const apps = await route.default(makeEvent())

    expect(apps).toEqual([{ id: 'home', name: 'Home', url: 'https://shell.example.test' }])
    expect(shell.requests[0]?.url).toBe('/api/portal/public/apps')
  })

  it('returns an empty list when the shell refuses', async () => {
    shell.respondWith(() => ({ status: 500, body: { detail: 'down' } }))

    expect(await route.default(makeEvent())).toEqual([])
  })

  it('returns an empty list when the shell answers something that is not a list', async () => {
    shell.respondWith(() => ({ body: { apps: [] } }))

    expect(await route.default(makeEvent())).toEqual([])
  })

  it('answers 404 where the widget is not enabled', async () => {
    setRuntimeConfig({ public: { authMode: 'local', mainUrl: shell.baseUrl } })

    expect(await refusal(() => route.default(makeEvent()))).toMatchObject({ statusCode: 404 })
    expect(shell.requests).toHaveLength(0)
  })
})
