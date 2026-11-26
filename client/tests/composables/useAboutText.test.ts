/**
 * Tests for `useAboutText`: the operator's story in the visitor's language, split into paragraphs.
 *
 * @module tests/composables/useAboutText.test
 */

import { describe, expect, it } from 'vitest'
import { useAboutText } from '~/composables/useAboutText'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { localizedSettingKey } from '~/utils/localizedSetting'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import { stubLocale } from '~/tests/helpers/runtimeStubs'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer()

describe('useAboutText', () => {
  it('splits the story at blank lines and trims each paragraph', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.AboutText]: ' First.\n\n  \nSecond line\nstill second. ' })

    expect(useAboutText().paragraphs.value).toEqual(['First.', 'Second line\nstill second.'])
  })

  it('reads the Russian story for a Russian visitor', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.AboutText]: 'English.', [localizedSettingKey(PortalSettingKey.AboutText, 'ru')]: 'Русский.' })
    stubLocale('ru')

    expect(useAboutText().paragraphs.value).toEqual(['Русский.'])
  })

  it('reads the Armenian story for an Armenian visitor', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.AboutText]: 'English.', [localizedSettingKey(PortalSettingKey.AboutText, 'hy')]: 'Հայերեն։' })
    stubLocale('hy')

    expect(useAboutText().paragraphs.value).toEqual(['Հայերեն։'])
  })

  it('reads the story written for a German visitor, a language the portal gained after the first three', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.AboutText]: 'English.', [localizedSettingKey(PortalSettingKey.AboutText, 'de')]: 'Deutsch.' })
    stubLocale('de')

    expect(useAboutText().paragraphs.value).toEqual(['Deutsch.'])
  })

  it('falls back to the default story when the language has none', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.AboutText]: 'English.' })
    stubLocale('hy')

    expect(useAboutText().paragraphs.value).toEqual(['English.'])
  })

  it('is empty when no story is set', async () => {
    await loadPortalSettings(replies)

    expect(useAboutText().paragraphs.value).toEqual([])
  })
})
