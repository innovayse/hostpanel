/**
 * Tests for `plugins/tracking.client.ts`: the tag manager loads once, only with a configured
 * container and only after the visitor accepted all cookies.
 *
 * @module tests/plugins/tracking.client.test
 */

import { nextTick, ref } from 'vue'
import type { Ref } from 'vue'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import { useCookieConsent } from '~/composables/useCookieConsent'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { CookieConsent } from '~/types/cookieconsent'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer({ useCookieConsent })

/** The plugin definition. */
interface PluginDefinition {
  /** Plugin name. */
  name: string
  /** Plugins it waits for. */
  dependsOn: string[]
  /** The setup function. */
  setup: () => void
}

/** The browser's cookie the real consent composable reads; `useCookie` is the Nuxt global that hands it out. */
let consent: Ref<CookieConsent | null>

/** The operator's settings the stub answers with, by their dotted key. */
let settings: Record<string, string> = {}

/**
 * Loads the plugin and runs its setup.
 *
 * @returns The plugin definition.
 */
const loadPlugin = async (): Promise<PluginDefinition> => {
  await loadPortalSettings(replies, settings)
  vi.stubGlobal('defineNuxtPlugin', (definition: PluginDefinition) => definition)
  vi.stubGlobal('useCookie', () => consent)
  vi.stubGlobal('watch', (await import('vue')).watch)
  const plugin = (await import('~/plugins/tracking.client')).default as unknown as PluginDefinition
  plugin.setup()
  return plugin
}

/**
 * Finds the injected tag manager scripts.
 *
 * @returns The script elements in `<head>`.
 */
const injected = (): HTMLScriptElement[] => {
  return Array.from(document.head.querySelectorAll<HTMLScriptElement>('script[src*="googletagmanager"]'))
}

beforeEach(() => {
  consent = ref<CookieConsent | null>(null)
  settings = { [PortalSettingKey.AnalyticsGtmId]: 'GTM-TEST' }
  vi.resetModules()
})

afterEach(() => {
  for (const script of injected()) {
    script.remove()
  }
})

describe('tracking plugin', () => {
  it('waits for the portal settings to load', async () => {
    const plugin = await loadPlugin()

    expect(plugin.name).toBe('tracking')
    expect(plugin.dependsOn).toEqual(['portal-settings'])
  })

  it('loads nothing before the visitor answered', async () => {
    await loadPlugin()

    expect(injected()).toHaveLength(0)
  })

  it('loads nothing when the visitor accepted essential cookies only', async () => {
    consent.value = CookieConsent.Essential
    await loadPlugin()

    expect(injected()).toHaveLength(0)
  })

  it('loads nothing when no container is configured', async () => {
    settings = {}
    consent.value = CookieConsent.All
    await loadPlugin()

    expect(injected()).toHaveLength(0)
  })

  it('injects the script once when all cookies are accepted', async () => {
    consent.value = CookieConsent.All
    await loadPlugin()

    expect(injected()).toHaveLength(1)
    expect(injected()[0]!.src).toContain('id=GTM-TEST')
  })

  it('injects the script when consent is given later, and only once', async () => {
    await loadPlugin()

    consent.value = CookieConsent.All
    await nextTick()
    consent.value = CookieConsent.Essential
    await nextTick()
    consent.value = CookieConsent.All
    await nextTick()

    expect(injected()).toHaveLength(1)
  })
})
