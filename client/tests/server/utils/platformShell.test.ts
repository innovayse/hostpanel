/**
 * Tests for `server/utils/platformShell.ts`: the widget is relayed only where it is enabled, a
 * script is cached for a few minutes, and a failed read falls back to the last copy.
 *
 * @module tests/server/utils/platformShell.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { refusal, setRuntimeConfig, startBackend } from '../../helpers/bffHarness'
import type { StubServer } from '../../helpers/stubServer'

/** The stub standing in for the platform shell. */
let shell: StubServer

/** The module under test, loaded fresh so each test starts with an empty cache. */
let platformShell: typeof import('~/server/utils/platformShell')

beforeEach(async () => {
  shell = await startBackend(() => ({ body: '/* widget */' }))
  setRuntimeConfig({ public: { authMode: 'sso', mainUrl: `${shell.baseUrl}/` } })
  vi.resetModules()
  platformShell = await import('~/server/utils/platformShell')
})

afterEach(async () => {
  vi.useRealTimers()
  await shell.close()
})

describe('requireShellUrl', () => {
  it('returns the configured address without its trailing slash', () => {
    expect(platformShell.requireShellUrl()).toBe(shell.baseUrl)
  })

  it.each([
    ['no address', { authMode: 'sso', mainUrl: '' }],
    ['local auth', { authMode: 'local', mainUrl: 'https://shell.example.test' }],
    ['a developer-machine host', { authMode: 'sso', mainUrl: 'http://app.local:3000' }]
  ])('answers 404 for %s', (_name, publicConfig) => {
    setRuntimeConfig({ public: publicConfig })

    expect(() => platformShell.requireShellUrl()).toThrow(expect.objectContaining({ statusCode: 404, data: { code: platformShell.SHELL_NOT_ENABLED } }))
  })
})

describe('readWidgetScript', () => {
  it('reads the script from the shell and reuses it inside the cache window', async () => {
    expect(await platformShell.readWidgetScript('header.v2.js')).toBe('/* widget */')
    expect(await platformShell.readWidgetScript('header.v2.js')).toBe('/* widget */')

    expect(shell.requests).toHaveLength(1)
    expect(shell.requests[0]?.url).toBe('/widget/header.v2.js')
  })

  it('keeps one copy per file', async () => {
    await platformShell.readWidgetScript('header.js')
    await platformShell.readWidgetScript('header.v2.js')

    expect(shell.requests.map(request => request.url)).toEqual(['/widget/header.js', '/widget/header.v2.js'])
  })

  it('asks the shell again once the copy is older than the cache window', async () => {
    const now = Date.now()
    const clock = vi.spyOn(Date, 'now').mockReturnValue(now)
    await platformShell.readWidgetScript('header.js')

    clock.mockReturnValue(now + platformShell.SHELL_CACHE_MS + 1)
    shell.respondWith(() => ({ body: '/* newer */' }))

    expect(await platformShell.readWidgetScript('header.js')).toBe('/* newer */')
    expect(shell.requests).toHaveLength(2)
    clock.mockRestore()
  })

  it('serves the last copy when the shell cannot be read', async () => {
    const now = Date.now()
    const clock = vi.spyOn(Date, 'now').mockReturnValue(now)
    await platformShell.readWidgetScript('header.js')

    clock.mockReturnValue(now + platformShell.SHELL_CACHE_MS + 1)
    shell.respondWith(() => ({ status: 500, body: 'down' }))

    expect(await platformShell.readWidgetScript('header.js')).toBe('/* widget */')
    clock.mockRestore()
  })

  it('answers 502 with a code when the shell cannot be read and nothing is cached', async () => {
    shell.respondWith(() => ({ status: 500, body: 'down' }))

    const error = await refusal(() => platformShell.readWidgetScript('header.js'))

    expect(error).toMatchObject({ statusCode: 502, data: { code: platformShell.SHELL_UNAVAILABLE } })
  })

  it('offers only the two known files', () => {
    expect([...platformShell.WIDGET_FILES].sort()).toEqual(['header.js', 'header.v2.js'])
  })
})
