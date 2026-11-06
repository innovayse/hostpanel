/**
 * Tests for `plugins/header-widget.ts`: the shared header script is requested from this portal's own
 * origin (relayed by `server/routes/widget`), and not at all when no shell is configured.
 *
 * @module tests/plugins/header-widget.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'

/** The captured `useHead` call. */
const useHeadMock = vi.fn()

/**
 * Loads the plugin with the given portal URL and runs it.
 *
 * @param mainUrl - The configured portal URL.
 * @param authMode - The deployment's auth mode; `sso` unless a test says otherwise.
 */
const runPlugin = async (mainUrl: string, authMode: string = 'sso'): Promise<void> => {
  vi.stubGlobal('defineNuxtPlugin', (setup: () => void) => setup)
  vi.stubGlobal('useRuntimeConfig', () => ({ public: { mainUrl, authMode } }))
  vi.stubGlobal('useHead', useHeadMock)
  /** The plugin as Nuxt calls it. */
  const plugin = (await import('~/plugins/header-widget')).default as unknown as () => void
  plugin()
}

beforeEach(() => {
  useHeadMock.mockClear()
  vi.resetModules()
})

describe('header-widget plugin', () => {
  it("adds the deferred widget script from this portal's own origin, so the widget builds its addresses here", async () => {
    await runPlugin('https://main.example.test')

    const head = useHeadMock.mock.calls[0]![0] as { script: Array<{ src: string, defer: boolean }> }
    expect(head.script).toHaveLength(1)
    expect(head.script[0]!.src.startsWith('/widget/header.js?v=')).toBe(true)
    expect(head.script[0]!.defer).toBe(true)
  })

  it('adds nothing when no portal is configured', async () => {
    await runPlugin('')

    expect(useHeadMock).not.toHaveBeenCalled()
  })

  it('adds nothing for the developer-machine default host', async () => {
    await runPlugin('http://portal.local:3000')

    expect(useHeadMock).not.toHaveBeenCalled()
  })

  it('adds nothing under local auth, where there is no SSO for the widget to reach', async () => {
    await runPlugin('https://main.example.test', 'local')

    expect(useHeadMock).not.toHaveBeenCalled()
  })
})
