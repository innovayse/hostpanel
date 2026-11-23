/**
 * Tests for `plugins/live-chat.ts`: the loader script is emitted only when provider, server and
 * token are all set, and the token follows the locale.
 *
 * @module tests/plugins/live-chat.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ref } from 'vue'
import type { Ref } from 'vue'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { ChatProvider } from '~/types/chatprovider'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer()

/** One script tag as handed to `useHead`. */
interface ScriptTag {
  /** The inline source. */
  innerHTML: string
  /** Where the tag is placed. */
  tagPosition: string
}

/** The head input the plugin registers. */
interface HeadInput {
  /** The script factory. */
  script: () => ScriptTag[]
}

/** The plugin definition. */
interface PluginDefinition {
  /** Plugin name. */
  name: string
  /** Plugins it waits for. */
  dependsOn: string[]
  /** The setup function. */
  setup: (app: { $i18n: { locale: Ref<string> } }) => void
}

/** The operator's settings the stub answers with, by their dotted key. */
let settings: Record<string, string> = {}

/** The captured `useHead` call. */
const useHeadMock = vi.fn()

/** The active locale. */
let locale: Ref<string>

/**
 * Loads the plugin and runs its setup.
 *
 * @returns The plugin definition.
 */
const loadPlugin = async (): Promise<PluginDefinition> => {
  await loadPortalSettings(replies, settings)
  vi.stubGlobal('defineNuxtPlugin', (definition: PluginDefinition) => definition)
  vi.stubGlobal('useHead', useHeadMock)
  vi.stubGlobal('watch', vi.fn())
  const plugin = (await import('~/plugins/live-chat')).default as unknown as PluginDefinition
  plugin.setup({ $i18n: { locale } })
  return plugin
}

/**
 * Reads the scripts the plugin currently asks for.
 *
 * @returns The script tags.
 */
const scripts = (): ScriptTag[] => {
  return (useHeadMock.mock.calls[0]![0] as HeadInput).script()
}

/**
 * Fills the settings a fully configured chat needs.
 */
const configureChat = (): void => {
  settings[PortalSettingKey.ChatProvider] = ChatProvider.Innochat
  settings[PortalSettingKey.ChatBaseUrl] = 'https://chat.example'
  settings[PortalSettingKey.ChatWebsiteToken] = 'tok-default'
}

beforeEach(() => {
  settings = {}
  locale = ref('en')
  useHeadMock.mockClear()
  vi.resetModules()
})

describe('live-chat plugin', () => {
  it('waits for the portal settings to load', async () => {
    const plugin = await loadPlugin()

    expect(plugin.name).toBe('live-chat')
    expect(plugin.dependsOn).toEqual(['portal-settings'])
  })

  it('emits no script when nothing is configured', async () => {
    await loadPlugin()

    expect(scripts()).toEqual([])
  })

  it('emits no script when the token is missing', async () => {
    configureChat()
    settings[PortalSettingKey.ChatWebsiteToken] = ''
    await loadPlugin()

    expect(scripts()).toEqual([])
  })

  it('emits no script when the token key is absent from the settings', async () => {
    configureChat()
    Reflect.deleteProperty(settings, PortalSettingKey.ChatWebsiteToken)
    await loadPlugin()

    expect(scripts()).toEqual([])
  })

  it('emits the loader at the end of the body when fully configured', async () => {
    configureChat()
    await loadPlugin()

    const tags = scripts()
    expect(tags).toHaveLength(1)
    expect(tags[0]!.tagPosition).toBe('bodyClose')
    expect(tags[0]!.innerHTML).toContain('tok-default')
    expect(tags[0]!.innerHTML).toContain('https://chat.example')
  })

  it('names the language of a Spanish visitor on the conversation', async () => {
    configureChat()
    locale.value = 'es'
    await loadPlugin()

    expect(scripts()[0]!.innerHTML).toContain('Spanish')
  })

  it('names the language of a German visitor on the conversation', async () => {
    configureChat()
    locale.value = 'de'
    await loadPlugin()

    expect(scripts()[0]!.innerHTML).toContain('German')
  })

  it('names the language of a French visitor on the conversation', async () => {
    configureChat()
    locale.value = 'fr'
    await loadPlugin()

    expect(scripts()[0]!.innerHTML).toContain('French')
  })

  it('prefers the per-locale token over the default one', async () => {
    configureChat()
    settings[PortalSettingKey.ChatWebsiteTokenRu] = 'tok-ru'
    locale.value = 'ru'
    await loadPlugin()

    expect(scripts()[0]!.innerHTML).toContain('tok-ru')
  })

  it('ignores an unsupported provider', async () => {
    configureChat()
    settings[PortalSettingKey.ChatProvider] = 'other'
    await loadPlugin()

    expect(scripts()).toEqual([])
  })
})
