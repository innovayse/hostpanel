/**
 * Tests for `useSeo`: the canonical URL, share image, Open Graph locale and head links a page
 * gets from the options it passes.
 *
 * @module tests/composables/useSeo.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ref } from 'vue'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { SeoPageType } from '~/types/seopagetype'
import { useSeo } from '~/composables/useSeo'
import type { SeoOptions } from '~/composables/useSeo'
import { useSiteIdentity } from '~/composables/useSiteIdentity'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import { stubLocale } from '~/tests/helpers/runtimeStubs'

/** The base URL of the stood-in site. */
const BASE = 'https://host.test'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer({ useSiteIdentity })

/** Spy for `useSeoMeta`, to read the tags the composable set. */
const useSeoMeta = vi.fn()

/** Spy for `useHead`, to read the links the composable set. */
const useHead = vi.fn()

/**
 * Names the site, stubs the Nuxt globals `useSeo` reads and records the head calls.
 *
 * @param locale The active language.
 * @param routePath The path of the current route.
 * @returns Resolves once the operator's settings are loaded.
 */
const stubRuntime = async (locale = 'en', routePath = '/hosting'): Promise<void> => {
  stubLocale(locale)
  vi.stubGlobal('useRuntimeConfig', () => ({ public: { baseUrl: BASE } }))
  vi.stubGlobal('useRoute', () => ({ path: routePath }))
  vi.stubGlobal('useSeoMeta', useSeoMeta)
  vi.stubGlobal('useHead', useHead)
  await loadPortalSettings(replies, { [PortalSettingKey.SiteName]: 'Site' })
}

/**
 * Reads the first argument of the single `useSeoMeta` call.
 *
 * @returns The meta tags that were set.
 */
const metaSet = (): Record<string, unknown> =>
  Object.fromEntries(
    Object.entries(useSeoMeta.mock.calls[0]![0] as Record<string, unknown>)
      .map(([name, value]) => [name, typeof value === 'function' ? (value as () => unknown)() : value]),
  )

/**
 * Reads the first argument of the single `useHead` call.
 *
 * @returns The head input the composable set.
 */
const headSet = (): { meta: unknown[], link: Array<{ rel: string, hreflang?: string, href: string }> } =>
  (useHead.mock.calls[0]![0] as () => ReturnType<typeof headSet>)()

describe('useSeo', () => {
  beforeEach(() => {
    useSeoMeta.mockClear()
    useHead.mockClear()
  })

  it('builds the canonical URL from the route path in English', async () => {
    await stubRuntime('en', '/hosting')

    const { canonicalUrl, baseUrl } = useSeo({})

    expect(baseUrl).toBe(BASE)
    expect(canonicalUrl.value).toBe(`${BASE}/hosting`)
  })

  it('prefixes the canonical URL with the language outside English and strips a prefix already on the path', async () => {
    await stubRuntime('ru', '/ru/hosting')

    expect(useSeo({}).canonicalUrl.value).toBe(`${BASE}/ru/hosting`)
  })

  it('prefers the path the page passes over the route path', async () => {
    await stubRuntime('en', '/hosting')

    expect(useSeo({ path: '/domains' }).canonicalUrl.value).toBe(`${BASE}/domains`)
  })

  it('sets the title, description and keywords for every card', async () => {
    await stubRuntime()
    const options: SeoOptions = { title: 'T', description: 'D', keywords: 'k' }

    useSeo(options)

    expect(metaSet()).toMatchObject({
      title: 'T',
      description: 'D',
      ogTitle: 'T',
      ogDescription: 'D',
      twitterTitle: 'T',
      twitterDescription: 'D',
      twitterCard: 'summary_large_image',
    })
    expect(headSet().meta).toEqual([{ name: 'keywords', content: 'k' }])
  })

  it('adds no keywords tag when the page gives none', async () => {
    await stubRuntime()

    useSeo({ title: 'T' })

    expect(headSet().meta).toEqual([])
  })

  it('uses the default share image unless the page supplies one', async () => {
    await stubRuntime()
    useSeo({})
    expect(metaSet()).toMatchObject({
      ogImage: `${BASE}/og-image.jpg`,
      twitterImage: `${BASE}/og-image.jpg`,
      ogImageWidth: 1200,
      ogImageHeight: 630,
      ogImageType: 'image/jpeg',
      ogImageAlt: 'Site',
    })

    useSeoMeta.mockClear()
    useSeo({ image: '/img/x.png' })
    expect(metaSet()).toMatchObject({ ogImage: `${BASE}/img/x.png`, twitterImage: `${BASE}/img/x.png` })
  })

  it('defaults the Open Graph type to website and honours an explicit one', async () => {
    await stubRuntime()
    useSeo({})
    expect(metaSet().ogType).toBe(SeoPageType.Website)

    useSeoMeta.mockClear()
    useSeo({ type: SeoPageType.Article })
    expect(metaSet().ogType).toBe(SeoPageType.Article)
  })

  it.each([
    ['en', 'en_US'],
    ['ru', 'ru_RU'],
    ['hy', 'hy_AM'],
    ['es', 'es_ES'],
    ['fr', 'en_US'],
  ])('maps the %s language to the %s Open Graph locale', async (locale, expected) => {
    await stubRuntime(locale)

    useSeo({})

    expect(metaSet().ogLocale).toBe(expected)
  })

  it('follows a getter, so tags written after the content arrives are current', async () => {
    await stubRuntime('en', '/knowledgebase/5')
    /** The title the getter reads; the test changes it after the first call. */
    const title = ref('Before')

    useSeo(() => ({ title: title.value, description: 'D', type: SeoPageType.Article }))
    expect(metaSet()).toMatchObject({ title: 'Before', ogTitle: 'Before', twitterTitle: 'Before', ogType: SeoPageType.Article })

    title.value = 'After'
    const getters = useSeoMeta.mock.calls[0]![0] as Record<string, () => unknown>
    expect(getters.title!()).toBe('After')
    expect(getters.ogImageAlt!()).toBe('After')
  })

  it('adds the canonical link and one alternate per language to the head', async () => {
    await stubRuntime('en', '/hosting')

    useSeo({})

    const links = headSet().link
    expect(links[0]).toEqual({ rel: 'canonical', href: `${BASE}/hosting` })
    expect(links.filter((link) => link.rel === 'alternate').map((link) => link.hreflang)).toEqual(
      expect.arrayContaining(['en', 'ru', 'hy', 'es', 'x-default']),
    )
  })
})
