/**
 * Answers the operator's settings from the stub server, so a test of a section, a composable or a page that
 * reads them runs the real `usePortalSettings` instead of a fake of it.
 *
 * In the app the `portal-settings` plugin loads `GET /api/portal/public/settings` into the shared
 * `portalSettings` state once per request, and `usePortalSettings` reads that state. This helper plays the
 * same two steps: the stub answers the read, the real plugin loads it, and the real composable (registered
 * under its auto-import name, as Nuxt does) reads what arrived. Only the Nuxt globals that do not exist in a
 * unit test are stood in for: `defineNuxtPlugin`, which only returns its definition.
 *
 * {@link usePortalSettingsServer} is the one setup a test file calls to get the stub, the reply table and the
 * real composables it reads; {@link homeSectionSettings} builds the home-section switches.
 *
 * @module tests/helpers/portalSettings
 */

import { afterEach, beforeEach, vi } from 'vitest'
import { usePortalSettings } from '~/composables/usePortalSettings'
import { HOME_SECTION_SETTING } from '~/constants/homeSection'
import type { HomeSection } from '~/constants/homeSection'
import { stubLocalePath } from './runtimeStubs'
import { readOf, routeTable } from './stubRoutes'
import type { StubRouteTable } from './stubRoutes'
import type { StubServer } from './stubServer'
import { startTransport } from './stubTransport'

/** The BFF path of the public settings read. */
const SETTINGS_PATH = '/api/portal/public/settings'

/** What the plugin module exports once `defineNuxtPlugin` has returned its definition unchanged. */
interface PluginDefinition {
  /** Loads the settings into the shared state. */
  setup: () => Promise<void>
}

/**
 * Makes the stub serve these settings and loads them through the real plugin, so the real
 * `usePortalSettings` reads them. Call it again with other settings to change what the next mount reads.
 *
 * The stub transport must already be running, because the plugin swallows a failed read and
 * leaves the settings empty, which would let a test that expects empty settings pass for the wrong reason.
 *
 * @param replies - The reply table of the running stub; its settings route is set here.
 * @param settings - The operator's settings by their dotted key; a key that is absent reads as unset.
 * @returns Resolves once the plugin has put the answer into the shared state.
 */
export const loadPortalSettings = async (replies: StubRouteTable, settings: Record<string, string> = {}): Promise<void> => {
  replies[readOf(SETTINGS_PATH)] = { body: settings }
  vi.stubGlobal('defineNuxtPlugin', (definition: PluginDefinition) => definition)
  vi.stubGlobal('usePortalSettings', usePortalSettings)
  // The plugin keeps settings that are already loaded, so a second call starts from an empty state.
  useState<Record<string, string>>('portalSettings', () => ({})).value = {}
  const plugin = (await import('~/plugins/portal-settings')).default as unknown as PluginDefinition
  await plugin.setup()
}

/** What {@link usePortalSettingsServer} hands the test file. */
export interface PortalSettingsServer {
  /** What the stub answers, by route; pass it to {@link loadPortalSettings} or add the reads a test needs. */
  replies: StubRouteTable
  /**
   * The stub of the running test, for the helpers that wait on its reads.
   *
   * @returns The stub started for the current test.
   * @throws When it is called outside a test, before its hooks have started the stub.
   */
  stub: () => StubServer
}

/**
 * Registers the hooks every test of a section that reads the operator's settings needs: a stub server under
 * `$fetch` and a localised-path stand-in before each test, and the stub closed, its replies cleared and the globals restored after it.
 *
 * Call it once at the top of the test file, then load the settings with {@link loadPortalSettings}.
 *
 * @param composables - The real composables the section calls, by the auto-import name Nuxt gives them. Registering
 * the real composable under its own name replaces nothing, it only makes it reachable as the global it is in the app.
 * The name is dynamic here, so the `noOwnMocks` guard reads the argument at each call site and refuses any entry that
 * is not a bare imported identifier.
 * @returns The reply table and the accessor of the stub the hooks start.
 */
export const usePortalSettingsServer = (composables: Record<string, unknown> = {}): PortalSettingsServer => {
  /** The stub of the running test, undefined between tests. */
  let running: StubServer | undefined
  /** What the stub answers, by route. */
  const replies: StubRouteTable = {}

  beforeEach(async () => {
    running = await startTransport(routeTable(replies))
    stubLocalePath()
    for (const [name, composable] of Object.entries(composables)) {
      vi.stubGlobal(name, composable)
    }
  })

  afterEach(async () => {
    await running?.close()
    running = undefined
    for (const route of Object.keys(replies)) {
      Reflect.deleteProperty(replies, route)
    }
    vi.unstubAllGlobals()
  })

  return {
    replies,
    stub: () => {
      if (!running) {
        throw new Error('The portal settings stub runs only inside a test.')
      }
      return running
    },
  }
}

/**
 * Maps home-page sections to one stored switch value, keyed by the setting that holds each switch.
 *
 * @param sections - The sections to write.
 * @param value - The stored value to give each, `SETTING_ON` or `SETTING_OFF`.
 * @returns The settings by their dotted key, ready for {@link loadPortalSettings}.
 */
export const homeSectionSettings = (sections: Iterable<HomeSection>, value: string): Record<string, string> =>
  Object.fromEntries([...sections].map((section) => [HOME_SECTION_SETTING[section], value]))
