/**
 * Tests for `plugins/brand-tokens.ts`: the operator's colours reach `<head>` as one critical
 * style tag, and nothing is emitted when no setting is set.
 *
 * @module tests/plugins/brand-tokens.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import { PortalSettingKey } from '~/constants/portalSettingKey'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer()

/** One style tag as handed to `useHead`. */
interface StyleTag {
  /** Element id. */
  id: string
  /** The CSS text. */
  innerHTML: string
  /** Head ordering. */
  tagPriority: string
}

/** The head input the plugin registers. */
interface HeadInput {
  /** The style factory. */
  style: () => StyleTag[]
}

/** The plugin definition. */
interface PluginDefinition {
  /** Plugin name. */
  name: string
  /** Plugins it waits for. */
  dependsOn: string[]
  /** The setup function. */
  setup: () => void
}

/** The operator's settings the stub answers with, by their dotted key. */
let settings: Record<string, string> = {}

/** The captured `useHead` call. */
const useHeadMock = vi.fn()

/**
 * Loads the plugin and runs its setup.
 *
 * @returns The plugin definition.
 */
const loadPlugin = async (): Promise<PluginDefinition> => {
  await loadPortalSettings(replies, settings)
  vi.stubGlobal('defineNuxtPlugin', (definition: PluginDefinition) => definition)
  vi.stubGlobal('useHead', useHeadMock)
  const plugin = (await import('~/plugins/brand-tokens')).default as unknown as PluginDefinition
  plugin.setup()
  return plugin
}

beforeEach(() => {
  settings = {}
  useHeadMock.mockClear()
  vi.resetModules()
})

describe('brand-tokens plugin', () => {
  it('waits for the portal settings to load', async () => {
    const plugin = await loadPlugin()

    expect(plugin.name).toBe('brand-tokens')
    expect(plugin.dependsOn).toEqual(['portal-settings'])
  })

  it('emits no tag when no brand setting is set', async () => {
    await loadPlugin()

    const head = useHeadMock.mock.calls[0]![0] as HeadInput
    expect(head.style()).toEqual([])
  })

  it('emits one critical style tag carrying the brand variables', async () => {
    settings[PortalSettingKey.BrandPrimary] = '#336699'
    await loadPlugin()

    const head = useHeadMock.mock.calls[0]![0] as HeadInput
    const tags = head.style()
    expect(tags).toHaveLength(1)
    expect(tags[0]!.id).toBe('brand-tokens')
    expect(tags[0]!.tagPriority).toBe('critical')
    expect(tags[0]!.innerHTML.startsWith(':root{')).toBe(true)
  })
})
