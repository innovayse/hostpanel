/**
 * Tests for `useLocalizedSetting`: the per-language row when the visitor's language has one,
 * the default row otherwise.
 *
 * @module tests/composables/useLocalizedSetting.test
 */

import { describe, expect, it } from 'vitest'
import { ownRowFor, useLocalizedSetting } from '~/composables/useLocalizedSetting'
import type { LocalizedSettingSpec } from '~/composables/useLocalizedSetting'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import { stubLocale } from '~/tests/helpers/runtimeStubs'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer()

/** The office address rows, used as the sample setting. */
const ADDRESS: LocalizedSettingSpec = {
  default: { key: PortalSettingKey.ContactAddress, envKey: 'portalContactAddress' },
  ru: { key: PortalSettingKey.ContactAddressRu, envKey: 'portalContactAddressRu' },
  hy: { key: PortalSettingKey.ContactAddressHy, envKey: 'portalContactAddressHy' }
}

describe('useLocalizedSetting', () => {
  it('reads the default row for English', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ContactAddress]: 'Default', [PortalSettingKey.ContactAddressRu]: 'Русский' })
    stubLocale('en')

    expect(useLocalizedSetting().getLocalized(ADDRESS)).toBe('Default')
  })

  it('reads the Russian row for a Russian visitor', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ContactAddress]: 'Default', [PortalSettingKey.ContactAddressRu]: 'Русский' })
    stubLocale('ru')

    expect(useLocalizedSetting().getLocalized(ADDRESS)).toBe('Русский')
  })

  it('reads the Armenian row for an Armenian visitor', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ContactAddress]: 'Default', [PortalSettingKey.ContactAddressHy]: 'Հայերեն' })
    stubLocale('hy')

    expect(useLocalizedSetting().getLocalized(ADDRESS)).toBe('Հայերեն')
  })

  it('falls back to the default row when the language has none', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.ContactAddress]: 'Default' })
    stubLocale('ru')

    expect(useLocalizedSetting().getLocalized(ADDRESS)).toBe('Default')
  })

  it('is empty when nothing is set', async () => {
    await loadPortalSettings(replies)
    stubLocale('hy')

    expect(useLocalizedSetting().getLocalized(ADDRESS)).toBe('')
  })
})

describe('ownRowFor', () => {
  /** The rows of a setting stored once per language. */
  const rows = { ru: 'russian row', hy: 'armenian row' }

  it('picks the Russian and the Armenian row for those languages', () => {
    expect(ownRowFor('ru', rows)).toBe('russian row')
    expect(ownRowFor('hy', rows)).toBe('armenian row')
  })

  it('has no row of its own for English or any other language, which read the default row', () => {
    expect(ownRowFor('en', rows)).toBeUndefined()
    expect(ownRowFor('de', rows)).toBeUndefined()
  })
})
