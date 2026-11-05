/**
 * Tests for `GET /widget/:file`: the shell's header widget is served from this portal's own origin,
 * which is what makes its account-switch and add-account addresses this portal's own.
 *
 * @module tests/server/routes/widget/[file].get.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { makeEvent, refusal, setRuntimeConfig, startBackend } from '../../../helpers/bffHarness'
import type { RouteModule } from '../../../helpers/bffHarness'
import type { StubServer } from '../../../helpers/stubServer'

/** The stub standing in for the platform shell. */
let shell: StubServer

/** The route under test. */
let route: RouteModule

beforeEach(async () => {
  shell = await startBackend(() => ({ body: '(function(){/* widget */})()' }))
  setRuntimeConfig({ public: { authMode: 'sso', mainUrl: shell.baseUrl } })
  vi.resetModules()
  route = await import('~/server/routes/widget/[file].get')
})

afterEach(async () => {
  await shell.close()
})

describe('GET /widget/:file', () => {
  it.each(['header.js', 'header.v2.js'])('relays %s as a script with the headers a script needs', async (file) => {
    const event = makeEvent({ params: { file } })

    const body = await route.default(event)

    expect(body).toBe('(function(){/* widget */})()')
    expect(shell.requests[0]?.url).toBe(`/widget/${file}`)
    expect(event.responseHeaders).toMatchObject({
      'Content-Type': 'text/javascript; charset=utf-8',
      'X-Content-Type-Options': 'nosniff'
    })
  })

  it.each(['../secret', 'header.v3.js', 'index.html', ''])('answers 404 for the name "%s" without asking the shell', async (file) => {
    const error = await refusal(() => route.default(makeEvent({ params: { file } })))

    expect(error).toMatchObject({ statusCode: 404, data: { code: 'WIDGET_FILE_NOT_FOUND' } })
    expect(shell.requests).toHaveLength(0)
  })

  it('answers 404 where the widget is not enabled', async () => {
    setRuntimeConfig({ public: { authMode: 'local', mainUrl: shell.baseUrl } })

    const error = await refusal(() => route.default(makeEvent({ params: { file: 'header.js' } })))

    expect(error).toMatchObject({ statusCode: 404, data: { code: 'SHELL_NOT_ENABLED' } })
    expect(shell.requests).toHaveLength(0)
  })
})
