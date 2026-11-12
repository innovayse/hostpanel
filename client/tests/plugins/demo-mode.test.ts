/**
 * Tests for `plugins/demo-mode.ts`: the demo status is loaded through the real demo store, API
 * composable and transport (against a stub server) when the plugin's setup runs, and the plugin is
 * named so others can depend on it.
 *
 * @module tests/plugins/demo-mode.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'

/** Path of the endpoint that reports the demo status. */
const DEMO_PATH = '/api/portal/demo'

/** The stub playing the BFF. */
let stub: StubServer

/** The plugin definition. */
interface PluginDefinition {
  /** Plugin name. */
  name: string
  /** The setup function. */
  setup: () => Promise<void>
}

/**
 * Loads the plugin definition.
 *
 * @returns The plugin definition.
 */
const loadPlugin = async (): Promise<PluginDefinition> => {
  vi.stubGlobal('defineNuxtPlugin', (definition: PluginDefinition) => definition)
  return (await import('~/plugins/demo-mode')).default as unknown as PluginDefinition
}

beforeEach(async () => {
  setActivePinia(createPinia())
  stub = await startTransport(routeTable({ [`GET ${DEMO_PATH}`]: { body: { enabled: true, nextResetAt: null, accounts: [] } } }))
  vi.resetModules()
})

afterEach(async () => {
  await stub.close()
  vi.unstubAllGlobals()
})

describe('demo-mode plugin', () => {
  it('is named so other plugins can depend on it', async () => {
    const plugin = await loadPlugin()

    expect(plugin.name).toBe('demo-mode')
  })

  it('loads the demo status once on setup', async () => {
    const plugin = await loadPlugin()

    await plugin.setup()

    expect(requestsTo(stub.requests, DEMO_PATH)).toHaveLength(1)
  })
})
