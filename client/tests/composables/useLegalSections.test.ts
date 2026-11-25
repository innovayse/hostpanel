/**
 * Tests for `useLegalSections`: the operator's text of a page, read from the legal text endpoint, replaces the
 * built-in sections in the visitor's language, and a page with no text keeps them. The real content store and
 * transport run against a stub BFF; only the visitor's language and the page's built-in sections are given.
 *
 * @module tests/composables/useLegalSections.test
 */

import { createPinia, setActivePinia } from 'pinia'
import { computed, ref } from 'vue'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useLegalSections } from '~/composables/useLegalSections'
import { LegalPage } from '~/constants/legalPage'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { localizedSettingKey } from '~/utils/localizedSetting'
import type { LegalSection } from '~/types/legalsection'
import { legalTextPath } from '~/tests/helpers/contentPaths'
import { readOf, requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'

/** The built-in sections a page hands over. */
const BUILT_IN = computed<LegalSection[]>(() => [{ id: 'acceptance', title: 'Built-in', paragraphs: ['Shipped text.'] }])

/** The language of the visitor, which a test sets. */
const locale = ref('en')

/** The stub playing the BFF. */
let stub: StubServer

/** What the stub answers, by route. */
let replies: StubRouteTable

/**
 * Makes the stub answer the legal text of one page.
 *
 * @param page The page.
 * @param rows The page's rows by their `portal.legal.*` key.
 */
const answer = (page: LegalPage, rows: Record<string, string>): void => {
  replies[readOf(legalTextPath(page))] = { body: rows }
}

/**
 * Reads the sections of one page.
 *
 * @param page The page.
 * @returns What the composable hands the page.
 */
const read = (page: LegalPage) => useLegalSections(page, BUILT_IN)

describe('useLegalSections', () => {
  beforeEach(async () => {
    setActivePinia(createPinia())
    replies = {}
    stub = await startTransport(routeTable(replies))
    locale.value = 'en'
    vi.stubGlobal('useI18n', () => ({ t: (key: string) => key, locale }))
  })

  afterEach(async () => {
    await stub.close()
    vi.unstubAllGlobals()
  })

  it('keeps the built-in sections when the operator set no text', async () => {
    answer(LegalPage.Terms, {})

    const { sections, isCustom } = await read(LegalPage.Terms)

    expect(isCustom.value).toBe(false)
    expect(sections.value).toEqual(BUILT_IN.value)
  })

  it('keeps the built-in sections when the read fails', async () => {
    const { sections, isCustom } = await read(LegalPage.Terms)

    expect(isCustom.value).toBe(false)
    expect(sections.value).toEqual(BUILT_IN.value)
  })

  it('shows the operator text instead of the built-in sections', async () => {
    answer(LegalPage.Terms, { [PortalSettingKey.LegalTerms]: '## Seller\nAcme Ltd.' })

    const { sections, isCustom } = await read(LegalPage.Terms)

    expect(isCustom.value).toBe(true)
    expect(sections.value).toEqual([{ id: 'custom-1', title: 'Seller', paragraphs: ['Acme Ltd.'], items: [] }])
  })

  it('reads only the page it was asked for, once, whatever the language', async () => {
    answer(LegalPage.Refund, { [PortalSettingKey.LegalRefund]: 'Refunds in 14 days.' })

    expect((await read(LegalPage.Refund)).isCustom.value).toBe(true)

    expect(stub.requests.map(request => request.url)).toEqual([legalTextPath(LegalPage.Refund)])
    expect(requestsTo(stub.requests, legalTextPath(LegalPage.Terms))).toHaveLength(0)
  })

  it('titles text written before any heading with the page title', async () => {
    answer(LegalPage.Privacy, { [PortalSettingKey.LegalPrivacy]: 'We keep little data.' })

    expect((await read(LegalPage.Privacy)).sections.value[0]?.title).toBe('privacy.title')
  })

  it('reads the text of the visitor language, whichever portal language it is', async () => {
    answer(LegalPage.Cookies, {
      [PortalSettingKey.LegalCookies]: 'English.',
      [localizedSettingKey(PortalSettingKey.LegalCookies, 'ru')]: 'Русский.',
      [localizedSettingKey(PortalSettingKey.LegalCookies, 'hy')]: 'Հայերեն։',
      [localizedSettingKey(PortalSettingKey.LegalCookies, 'fr')]: 'Français.'
    })
    const { sections } = await read(LegalPage.Cookies)

    locale.value = 'ru'
    expect(sections.value[0]?.paragraphs).toEqual(['Русский.'])
    locale.value = 'hy'
    expect(sections.value[0]?.paragraphs).toEqual(['Հայերեն։'])
    locale.value = 'fr'
    expect(sections.value[0]?.paragraphs).toEqual(['Français.'])
    locale.value = 'en'
    expect(sections.value[0]?.paragraphs).toEqual(['English.'])
  })

  it('falls back to the default text for a language without its own, and to the built-in one when there is none', async () => {
    answer(LegalPage.AcceptableUse, { [PortalSettingKey.LegalAcceptableUse]: 'English only.' })
    locale.value = 'hy'

    expect((await read(LegalPage.AcceptableUse)).sections.value[0]?.paragraphs).toEqual(['English only.'])

    answer(LegalPage.Cookies, { [localizedSettingKey(PortalSettingKey.LegalCookies, 'ru')]: 'Только русский.' })
    expect((await read(LegalPage.Cookies)).sections.value).toEqual(BUILT_IN.value)
  })
})
