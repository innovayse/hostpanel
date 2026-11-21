/**
 * Tests for `useLegalSections`: the operator's text in the visitor's language replaces the built-in sections, and
 * a page with no text keeps them. The real settings reader runs against the operator's settings from the stub server.
 *
 * @module tests/composables/useLegalSections.test
 */

import { computed, ref } from 'vue'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useLegalSections } from '~/composables/useLegalSections'
import { LegalPage } from '~/constants/legalPage'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import type { LegalSection } from '~/types/legalsection'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer()

/** The built-in sections a page hands over. */
const BUILT_IN = computed<LegalSection[]>(() => [{ id: 'acceptance', title: 'Built-in', paragraphs: ['Shipped text.'] }])

/** The language of the visitor, which a test sets. */
const locale = ref('en')

/**
 * Reads the sections of one page.
 *
 * @param page The page.
 * @returns What the composable hands the page.
 */
const read = (page: LegalPage) => useLegalSections(page, BUILT_IN)

describe('useLegalSections', () => {
  beforeEach(() => {
    locale.value = 'en'
    vi.stubGlobal('useI18n', () => ({ t: (key: string) => key, locale }))
  })

  it('keeps the built-in sections when the operator set no text', async () => {
    await loadPortalSettings(replies)

    const { sections, isCustom } = read(LegalPage.Terms)

    expect(isCustom.value).toBe(false)
    expect(sections.value).toEqual(BUILT_IN.value)
  })

  it('shows the operator text instead of the built-in sections', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.LegalTerms]: '## Seller\nAcme Ltd.' })

    const { sections, isCustom } = read(LegalPage.Terms)

    expect(isCustom.value).toBe(true)
    expect(sections.value).toEqual([{ id: 'custom-1', title: 'Seller', paragraphs: ['Acme Ltd.'], items: [] }])
  })

  it('reads each page from its own settings', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.LegalRefund]: 'Refunds in 14 days.' })

    expect(read(LegalPage.Refund).isCustom.value).toBe(true)
    expect(read(LegalPage.Terms).isCustom.value).toBe(false)
    expect(read(LegalPage.Privacy).isCustom.value).toBe(false)
    expect(read(LegalPage.AcceptableUse).isCustom.value).toBe(false)
    expect(read(LegalPage.Cookies).isCustom.value).toBe(false)
  })

  it('titles text written before any heading with the page title', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.LegalPrivacy]: 'We keep little data.' })

    expect(read(LegalPage.Privacy).sections.value[0]?.title).toBe('privacy.title')
  })

  it('reads the Russian text for a Russian visitor and the Armenian one for an Armenian visitor', async () => {
    await loadPortalSettings(replies, {
      [PortalSettingKey.LegalCookies]: 'English.',
      [PortalSettingKey.LegalCookiesRu]: 'Русский.',
      [PortalSettingKey.LegalCookiesHy]: 'Հայերեն։'
    })

    locale.value = 'ru'
    expect(read(LegalPage.Cookies).sections.value[0]?.paragraphs).toEqual(['Русский.'])
    locale.value = 'hy'
    expect(read(LegalPage.Cookies).sections.value[0]?.paragraphs).toEqual(['Հայերեն։'])
  })

  it('falls back to the default text for a language without its own, and to the built-in one when there is none', async () => {
    await loadPortalSettings(replies, { [PortalSettingKey.LegalAcceptableUse]: 'English only.' })

    locale.value = 'hy'
    expect(read(LegalPage.AcceptableUse).sections.value[0]?.paragraphs).toEqual(['English only.'])

    await loadPortalSettings(replies, { [PortalSettingKey.LegalAcceptableUseRu]: 'Только русский.' })
    locale.value = 'hy'
    expect(read(LegalPage.AcceptableUse).sections.value).toEqual(BUILT_IN.value)
  })
})
