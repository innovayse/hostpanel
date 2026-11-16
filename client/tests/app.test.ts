/**
 * Tests for the root app component: the layout, the page slot and the cookie banner it renders,
 * the global structured data it injects, the document language, the site identity metas and the
 * favicon links it hands to the head.
 *
 * The real schema, site identity, contact, settings and favicon composables run against a stub server playing
 * the BFF; only the head, the locale and the layout and page slots are stood in for.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h, ref, unref } from 'vue'
import type { Ref } from 'vue'
import { documentHead } from '~/tests/helpers/documentHead'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import App from '~/app.vue'
import { useContactDetails } from '~/composables/useContactDetails'
import { useLocalizedSetting } from '~/composables/useLocalizedSetting'
import { useSchemaOrg } from '~/composables/useSchemaOrg'
import { useSiteIdentity } from '~/composables/useSiteIdentity'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { brandingIcons, toHeadIconLink } from '~/utils/brandingIcons'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer({ brandingIcons, toHeadIconLink, useContactDetails, useLocalizedSetting, useSchemaOrg, useSiteIdentity })

/** The operator-set site name the stub answers with. */
const SITE_NAME = 'Acme Hosting'

/** Spy for `useHead`, to read the language, the icon links and the structured data the app puts on the document. */
const useHead = vi.fn()

/** Spy for `useSeoMeta`, to read the identity metas the app sets. */
const useSeoMeta = vi.fn()

/** The page the root's `NuxtPage` shows. */
const PAGE_TESTID = 'page'

/**
 * Builds a component that renders its default slot inside a marked element.
 *
 * @param testid - The `data-testid` the element carries.
 * @returns The component.
 */
const slotMarker = (testid: string) => defineComponent({
  setup: (_props, { slots }) => () => h('div', { 'data-testid': testid }, slots.default?.()),
})

/**
 * Builds a component that renders a marked, empty element.
 *
 * @param testid - The `data-testid` the element carries.
 * @returns The component.
 */
const marker = (testid: string) => defineComponent({ setup: () => () => h('div', { 'data-testid': testid }) })

/**
 * Loads the operator's settings and stands in for the head and the locale the app reads.
 *
 * @param options - What the operator has set and the locale in use.
 * @param options.locale - The active UI locale.
 * @param options.tagline - The operator's tagline; empty means none.
 * @param options.favicon - The favicon setting.
 * @returns The locale ref, so a test can change it.
 */
const loadSettingsAndStubHead = async (options: { locale?: string, tagline?: string, favicon?: string } = {}): Promise<{ locale: Ref<string> }> => {
  /** The active UI locale. */
  const locale = ref(options.locale ?? 'en')
  await loadPortalSettings(replies, {
    [PortalSettingKey.SiteName]: SITE_NAME,
    [PortalSettingKey.SiteTagline]: options.tagline ?? '',
    [PortalSettingKey.Favicon]: options.favicon ?? '',
  })
  vi.stubGlobal('useI18n', () => ({ t: (key: string) => key, locale }))
  vi.stubGlobal('useHead', useHead)
  vi.stubGlobal('useSeoMeta', useSeoMeta)
  return { locale }
}

/**
 * Mounts the app with slot-rendering stand-ins for the layout and the page.
 *
 * @returns The wrapper.
 */
const mountApp = () => mountWithKit(App, {
  components: {
    NuxtLayout: slotMarker('layout'),
    NuxtPage: marker(PAGE_TESTID),
  },
})

/** What the app passes to `useHead` for the document: the language and the icon links. */
interface AppHead {
  /** The attributes of `<html>`. */
  htmlAttrs: { lang: () => string }
  /** The icon links. */
  link: Ref<Array<Record<string, string>>>
}

/**
 * Reads the object the app passed to `useHead` for the document: the language and the icon links.
 *
 * @returns The head input.
 */
const headInput = () => documentHead<AppHead>(useHead)

/**
 * Reads the structured data the app injected: the `@type` of each JSON-LD document, per injection.
 *
 * @returns One list of types for every `injectSchema` call the app made.
 */
const injectedSchemaTypes = (): string[][] =>
  useHead.mock.calls
    .map(([argument]) => argument)
    .filter((argument): argument is () => { script: Array<{ innerHTML: string }> } => typeof argument === 'function')
    .map((getter) => getter().script.map((tag) => (JSON.parse(tag.innerHTML) as { '@type': string })['@type']))

/**
 * Reads the object the app passed to `useSeoMeta`.
 *
 * @returns The identity metas, each a plain value or a ref.
 */
const seoMeta = () => useSeoMeta.mock.calls[0]![0] as Record<string, unknown>

describe('app.vue', () => {
  beforeEach(() => {
    useHead.mockReset()
    useSeoMeta.mockReset()
  })

  it('renders the layout around the page and leaves the cookie banner to the layouts', async () => {
    await loadSettingsAndStubHead()

    const wrapper = mountApp()

    expect(wrapper.find('[data-testid="layout"] [data-testid="page"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="cookie-banner"]').exists()).toBe(false)
  })

  it('injects the organization, local business and website schemas once', async () => {
    await loadSettingsAndStubHead()

    mountApp()

    expect(injectedSchemaTypes()).toEqual([['Organization', 'LocalBusiness', 'WebSite']])
  })

  it.each(['en', 'ru', 'hy'])('sets the document language to %s', async (code) => {
    await loadSettingsAndStubHead({ locale: code })

    mountApp()

    expect(headInput().htmlAttrs.lang()).toBe(code)
  })

  it('falls back to the default language for an unknown locale', async () => {
    await loadSettingsAndStubHead({ locale: 'xx' })

    mountApp()

    expect(headInput().htmlAttrs.lang()).toBe('en')
  })

  it('follows the locale when it changes', async () => {
    const { locale } = await loadSettingsAndStubHead({ locale: 'en' })

    mountApp()
    locale.value = 'hy'

    expect(headInput().htmlAttrs.lang()).toBe('hy')
  })

  it('sets the site name as og:site_name and author', async () => {
    await loadSettingsAndStubHead()

    mountApp()

    const meta = seoMeta()
    expect(unref(meta.ogSiteName)).toBe(SITE_NAME)
    expect(unref(meta.author)).toBe(SITE_NAME)
  })

  it('leaves the description alone when there is no tagline', async () => {
    await loadSettingsAndStubHead({ tagline: '' })

    mountApp()

    const meta = seoMeta()
    expect(meta).not.toHaveProperty('description')
    expect(meta).not.toHaveProperty('ogDescription')
  })

  it('sets the description and og:description from the tagline', async () => {
    await loadSettingsAndStubHead({ tagline: 'Hosting that stays up' })

    mountApp()

    const meta = seoMeta()
    expect(unref(meta.description)).toBe('Hosting that stays up')
    expect(unref(meta.ogDescription)).toBe('Hosting that stays up')
  })

  it('builds the favicon links from the favicon setting', async () => {
    await loadSettingsAndStubHead({ favicon: '/uploads/branding/favicon/abc/favicon.png' })

    mountApp()

    const hrefs = headInput().link.value.map((link) => link.href)
    expect(hrefs).toContain('/uploads/branding/favicon/abc/favicon-32x32.png')
    expect(hrefs).toContain('/uploads/branding/favicon/abc/apple-touch-icon.png')
  })

  it('passes neither type nor sizes for a pasted icon that has none', async () => {
    await loadSettingsAndStubHead({ favicon: 'https://cdn.example.test/bare.png' })

    mountApp()

    expect(headInput().link.value).toEqual([{ rel: 'icon', href: 'https://cdn.example.test/bare.png' }])
  })

  it('passes type and sizes for the icons of an uploaded favicon', async () => {
    await loadSettingsAndStubHead({ favicon: '/uploads/branding/favicon/abc/favicon.png' })

    mountApp()

    expect(headInput().link.value).toContainEqual({ rel: 'icon', href: '/uploads/branding/favicon/abc/favicon-32x32.png', type: 'image/png', sizes: '32x32' })
  })
})
