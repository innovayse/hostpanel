/**
 * Guard tests for the plugin capability labels: the admin lists exactly the capabilities the plugin SDK defines, and every one of
 * them has a label in every language, so the permissions list of a plugin never prints a raw message key.
 */
import { existsSync, readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { MessageLanguage, messages } from '@/locales/messages'
import { PluginCapability } from '@/types/plugin'
import { valueAtPath } from '@/lib/valueAtPath'

/** Where the plugin SDK keeps the capability names, relative to this test file. */
const SDK_CAPABILITIES = '../../../backend/src/Hostpanel.Sdk/Plugins/Manifest/PluginCapabilities.cs'

/** Message path of the label folder, below the area name. */
const LABELS = 'plugins.pluginInstallDialog.capabilities'

/**
 * Reads the capability names the SDK declares as constants.
 *
 * @returns The string value of every `public const string` of the SDK's `PluginCapabilities`.
 */
const sdkCapabilities = (): string[] => {
  const path = fileURLToPath(new URL(SDK_CAPABILITIES, import.meta.url))
  expect(existsSync(path), 'the plugin SDK submodule must be checked out').toBe(true)
  return [...readFileSync(path, 'utf8').matchAll(/public const string \w+ = "(\w+)";/g)].map((match) => match[1])
}

describe('PluginCapability', () => {
  it('lists exactly the capabilities the plugin SDK defines', () => {
    expect(Object.values(PluginCapability).sort()).toEqual(sdkCapabilities().sort())
  })

  it('has a label in every language for every capability, and no label for an unknown one', () => {
    const known = Object.values(PluginCapability).sort()
    for (const language of Object.values(MessageLanguage)) {
      const labels = valueAtPath(messages[language], LABELS) as Record<string, unknown>
      expect(Object.keys(labels).sort(), language).toEqual(known)
      for (const capability of known) {
        expect(typeof labels[capability], `${language}: ${capability}`).toBe('string')
        expect(labels[capability], `${language}: ${capability}`).not.toBe('')
      }
    }
  })
})
