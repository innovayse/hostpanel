/**
 * Tests for `plugins/color-mode.ts`: the `<html>` class, the theme-color meta and the legacy
 * storage migration.
 *
 * @module tests/plugins/color-mode.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Ref } from 'vue'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import { useAppColorMode } from '~/composables/useAppColorMode'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { TemplateName } from '~/types/templatename'
import { ColorMode } from '~/types/colormode'
import { ColorModeSystem } from '~/types/colormodesystem'

/** The head input the plugin registers. */
interface HeadInput {
  /** Attributes of `<html>`. */
  htmlAttrs: { class: () => string }
  /** Meta tags. */
  meta: Array<{ name: string, content: unknown, media?: string }>
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

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer({ useAppColorMode })

/** The mode on `<html>`: the real composable's shared state, which the plugin reads and writes. */
let applied: Ref<ColorMode | null>

/** The captured `useHead` call. */
const useHeadMock = vi.fn()

/** The operator's settings the stub answers with, by their dotted key. */
let settings: Record<string, string> = {}

/**
 * Loads the plugin and runs its setup.
 *
 * @returns The plugin definition.
 */
const loadPlugin = async (): Promise<PluginDefinition> => {
  await loadPortalSettings(replies, settings)
  applied = useAppColorMode().applied
  vi.stubGlobal('defineNuxtPlugin', (definition: PluginDefinition) => definition)
  vi.stubGlobal('useHead', useHeadMock)
  vi.stubGlobal('matchMedia', () => ({ addEventListener: vi.fn() }))
  const plugin = (await import('~/plugins/color-mode')).default as unknown as PluginDefinition
  plugin.setup()
  return plugin
}

beforeEach(() => {
  settings = {}
  useHeadMock.mockClear()
  localStorage.clear()
  vi.resetModules()
})

describe('color-mode plugin', () => {
  it('waits for the portal settings to load', async () => {
    const plugin = await loadPlugin()

    expect(plugin.name).toBe('color-mode')
    expect(plugin.dependsOn).toEqual(['portal-settings'])
  })

  it('writes the applied mode as the class on html', async () => {
    await loadPlugin()

    const head = useHeadMock.mock.calls[0]![0] as HeadInput
    expect(head.htmlAttrs.class()).toBe(ColorMode.Dark)
  })

  it('writes the template page colour into theme-color', async () => {
    settings[PortalSettingKey.ThemeDefault] = ColorMode.Light
    settings[PortalSettingKey.Template] = TemplateName.Nova
    await loadPlugin()

    const head = useHeadMock.mock.calls[0]![0] as HeadInput
    const themeColors = head.meta.filter(tag => tag.name === 'theme-color')
    expect(themeColors).toHaveLength(2)
    // A chosen mode wins whatever the device scheme.
    expect(themeColors.map(tag => (tag.content as Ref<string>).value)).toEqual(['#f4f8fa', '#f4f8fa'])
  })

  it('gives classic a light page colour of its own, so the toggle reaches the browser chrome', async () => {
    // Classic is the default template: no `portal.template` setting resolves to it.
    settings[PortalSettingKey.ThemeDefault] = ColorMode.Light
    await loadPlugin()

    const light = (useHeadMock.mock.calls[0]![0] as HeadInput).meta.filter(tag => tag.name === 'theme-color')
    expect(light.map(tag => (tag.content as Ref<string>).value)).toEqual(['#f4f5fa', '#f4f5fa'])

    applied.value = ColorMode.Dark
    expect(light.map(tag => (tag.content as Ref<string>).value)).toEqual(['#08090f', '#08090f'])
  })

  it('gives each device scheme its own theme-color while no mode is chosen', async () => {
    settings[PortalSettingKey.Template] = TemplateName.Nova
    await loadPlugin()
    // The browser half reads a mode back from <html>; clear it to stand for the server render.
    applied.value = null

    const head = useHeadMock.mock.calls[0]![0] as HeadInput
    const byMedia = Object.fromEntries(head.meta
      .filter(tag => tag.name === 'theme-color')
      .map(tag => [tag.media, (tag.content as Ref<string>).value]))
    expect(byMedia).toEqual({ '(prefers-color-scheme: light)': '#f4f8fa', '(prefers-color-scheme: dark)': '#08191f' })
  })

  it('migrates a legacy stored choice and removes the key', async () => {
    localStorage.setItem('color-mode', ColorMode.Light)

    await loadPlugin()

    expect(applied.value).toBe(ColorMode.Light)
    expect(localStorage.getItem('color-mode')).toBeNull()
  })

  it('reads the class the bootstrap script set when nothing was applied yet', async () => {
    settings[PortalSettingKey.ThemeDefault] = ColorModeSystem.System
    document.documentElement.classList.add(ColorMode.Light)

    await loadPlugin()

    expect(applied.value).toBe(ColorMode.Light)
    document.documentElement.classList.remove(ColorMode.Light)
  })
})
