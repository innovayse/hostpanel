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
 * @module tests/helpers/portalSettings
 */

import { vi } from 'vitest'
import { usePortalSettings } from '~/composables/usePortalSettings'
import { readOf } from './stubRoutes'
import type { StubRouteTable } from './stubRoutes'

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
