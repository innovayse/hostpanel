/**
 * Tests for `useLocalizedSetting`: the row of the visitor's language when it has text, the default row
 * otherwise, for every portal language alike.
 *
 * @module tests/composables/useLocalizedSetting.test
 */

import { describe, expect, it } from 'vitest'
import { readLocalized, useLocalizedSetting } from '~/composables/useLocalizedSetting'
import type { LocalizedSettingSpec } from '~/composables/useLocalizedSetting'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { localizedSettingKey } from '~/utils/localizedSetting'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import { stubLocale } from '~/tests/helpers/runtimeStubs'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer()

/** The office address rows, used as the sample setting. */
const ADDRESS: LocalizedSettingSpec = { key: PortalSettingKey.ContactAddress, envKey: 'portalContactAddress' }

describe('useLocalizedSetting', () => {
  it('reads the default row for English', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ContactAddress]: 'Default', [localizedSettingKey(PortalSettingKey.ContactAddress, 'ru')]: 'Русский' })
    stubLocale('en')

    expect(useLocalizedSetting().getLocalized(ADDRESS)).toBe('Default')
  })

  it.each([
    ['ru', 'Русский'],
    ['hy', 'Հայերեն'],
    ['de', 'Deutsch'],
    ['es', 'Español'],
    ['fr', 'Français']
  ])('reads the %s row for a visitor of that language', async (locale, text) => {
    await loadPortalSettings(replies, { [PortalSettingKey.ContactAddress]: 'Default', [localizedSettingKey(PortalSettingKey.ContactAddress, locale)]: text })
    stubLocale(locale)

    expect(useLocalizedSetting().getLocalized(ADDRESS)).toBe(text)
  })

  it('falls back to the default row when the language has none', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ContactAddress]: 'Default' })
    stubLocale('de')

    expect(useLocalizedSetting().getLocalized(ADDRESS)).toBe('Default')
  })

  it('is empty when nothing is set', async () => {
    await loadPortalSettings(replies)
    stubLocale('hy')

    expect(useLocalizedSetting().getLocalized(ADDRESS)).toBe('')
  })
})

describe('readLocalized', () => {
  /**
   * A reader over a fixed table, ignoring the runtime-config key.
   *
   * @param rows The stored settings by key.
   * @returns A reader that answers the row's text, or empty.
   */
  const reader = (rows: Record<string, string>) => (key: string): string => rows[key] ?? ''

  it('prefers the row of the language and falls back to the default one', () => {
    const get = reader({ 'portal.contact.address': 'Default', 'portal.contact.address.de': 'Deutsch' })

    expect(readLocalized(get, 'de', ADDRESS)).toBe('Deutsch')
    expect(readLocalized(get, 'es', ADDRESS)).toBe('Default')
    expect(readLocalized(get, 'en', ADDRESS)).toBe('Default')
  })

  it('names the runtime-config key of a language from the default one', () => {
    const asked: string[] = []
    readLocalized((_key, envKey) => {
      asked.push(envKey)
      return ''
    }, 'ru', ADDRESS)

    expect(asked).toEqual(['portalContactAddressRu', 'portalContactAddress'])
  })
})
