/**
 * Tests for the default layout: the active template's header and footer around the page slot,
 * the demo notice and the sign-in prompt, the floating actions and the cart drawer, and the template stamp on the document.
 *
 * Every component of ours runs for real: the template's header and footer, the language, currency and theme pickers,
 * the domain search, the demo notice, the sign-in prompt, the cookie banner, the floating actions and the cart drawer,
 * with the real catalogue, currency, demo and cart stores and the real composables against a stub server playing the
 * BFF. Only the head and the Nuxt router globals are stood in for.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { defineAsyncComponent, unref } from 'vue'
import { documentHead } from '~/tests/helpers/documentHead'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { CatalogPath } from '~/tests/helpers/catalogPaths'
import { awaitCatalogue } from '~/tests/helpers/payerCatalogue'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import { stubRoute, stubSwitchLocalePath } from '~/tests/helpers/runtimeStubs'
import { readOf, requestsTo } from '~/tests/helpers/stubRoutes'
import { waitUntil } from '~/tests/helpers/waitUntil'
import DefaultLayout from '~/layouts/default.vue'
import LayoutCartDrawer from '~/components/layout/CartDrawer.vue'
import LayoutFooter from '~/components/layout/Footer.vue'
import LayoutHeader from '~/components/layout/Header.vue'
import LayoutSignInPrompt from '~/components/layout/SignInPrompt.vue'
import SectionsDomainSearch from '~/components/sections/SectionsDomainSearch.vue'
import { useAppColorMode } from '~/composables/useAppColorMode'
import { useAuthMode } from '~/composables/useAuthMode'
import { useBrandLogo } from '~/composables/useBrandLogo'
import { useContactDetails } from '~/composables/useContactDetails'
import { useCookieConsent } from '~/composables/useCookieConsent'
import { useLocalizedSetting } from '~/composables/useLocalizedSetting'
import { useSiteIdentity } from '~/composables/useSiteIdentity'
import { useTemplate } from '~/composables/useTemplate'
import { SIGN_IN_PROMPT_QUERY, SIGN_IN_PROMPT_VALUE } from '~/constants/clientRoutes'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { useCartStore } from '~/stores/cart'
import { useDemoStore } from '~/stores/demo'
import { TemplateName } from '~/types/templatename'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies, stub } = usePortalSettingsServer({ useAppColorMode, useAuthMode, useBrandLogo, useContactDetails, useCookieConsent, useLocalizedSetting, useSiteIdentity, useTemplate })

/** Spy for `useHead`, to read what the layout stamps on the document. */
const useHead = vi.fn()

/** What a test can change about the mounted layout. */
interface MountOptions {
  /** The operator's settings by their dotted key; empty leaves the default template. */
  settings?: Record<string, string>
  /** Whether the visitor was sent back from the identity provider without a session, which shows the sign-in prompt. */
  signInRequired?: boolean
  /** Whether the deployment is a demo, which shows the demo notice. */
  demo?: boolean
}

describe('layouts/default', () => {
  beforeEach(() => {
    useHead.mockReset()
    vi.stubGlobal('useHead', useHead)
    vi.stubGlobal('defineAsyncComponent', defineAsyncComponent)
    stubSwitchLocalePath()
    replies[readOf(CatalogPath.ProductGroups)] = { body: [] }
    replies[readOf(CatalogPath.Products)] = { body: [] }
    replies[readOf(CatalogPath.BillingCurrencies)] = { body: [] }
  })

  /**
   * Mounts the layout with a page in its slot and waits for the template's header and footer to load.
   *
   * @param options - The settings, the sign-in prompt and the demo flag for this mount.
   * @returns The wrapper.
   */
  const mountLayout = async (options: MountOptions = {}) => {
    await loadPortalSettings(replies, options.settings ?? {})
    stubRoute(options.signInRequired ? { query: { [SIGN_IN_PROMPT_QUERY]: SIGN_IN_PROMPT_VALUE } } : {})
    const pinia = createPinia()
    setActivePinia(pinia)
    useDemoStore().enabled = options.demo ?? false
    const wrapper = mountWithKit(DefaultLayout, {
      pinia,
      slots: { default: '<span data-testid="page">Page</span>' },
      components: { LayoutCartDrawer, LayoutFooter, LayoutHeader, LayoutSignInPrompt, SectionsDomainSearch },
    })
    await waitUntil(() => {
      expect(wrapper.find('header').exists()).toBe(true)
      expect(wrapper.find('footer').exists()).toBe(true)
    })
    return wrapper
  }

  /**
   * Mounts the layout on the default (classic) template and lets the catalogue and the currencies its header, footer and
   * cart drawer read on mount settle, so the stub is not closed under a read.
   *
   * @param options - The sign-in prompt and the demo flag for this mount.
   * @returns The wrapper.
   */
  const mountClassicLayout = async (options: Pick<MountOptions, 'signInRequired' | 'demo'> = {}) => {
    const wrapper = await mountLayout(options)
    await waitUntil(() => expect(requestsTo(stub().requests, CatalogPath.ProductGroups).length).toBeGreaterThan(0))
    await waitUntil(() => expect(requestsTo(stub().requests, CatalogPath.BillingCurrencies).length).toBeGreaterThan(0))
    await awaitCatalogue()
    return wrapper
  }

  it('renders the template header, the page, the template footer, the floating actions and the cookie banner', async () => {
    const wrapper = await mountClassicLayout()

    for (const id of ['layout-header', 'page', 'layout-footer', 'floating-actions', 'cookie-banner']) {
      expect(wrapper.find(`[data-testid="${id}"]`).exists()).toBe(true)
    }
  })

  it('draws the real pickers and domain search inside the template footer', async () => {
    const wrapper = await mountClassicLayout()

    expect(wrapper.find('[data-testid="layout-footer"] [data-testid="language-switcher"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="layout-footer"] [data-testid="theme-toggle"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="layout-footer"] [data-testid="sections-domain-search"]').exists()).toBe(true)
  })

  it('shows the demo notice only on a demo deployment', async () => {
    expect((await mountClassicLayout()).find('[data-testid="demo-banner"]').exists()).toBe(false)
    expect((await mountClassicLayout({ demo: true })).find('[data-testid="demo-banner"]').exists()).toBe(true)
  })

  it('shows the sign-in prompt only to a visitor the identity provider sent back without a session', async () => {
    expect((await mountClassicLayout()).find('[data-testid="signin-prompt"]').exists()).toBe(false)
    expect((await mountClassicLayout({ signInRequired: true })).find('[data-testid="signin-prompt"]').exists()).toBe(true)
  })

  it('opens the real cart drawer when the cart is opened and keeps it out of the page while closed', async () => {
    const wrapper = await mountClassicLayout()
    expect(wrapper.find('[data-testid="ui-drawer"]').exists()).toBe(false)

    useCartStore().isOpen = true

    await waitUntil(() => expect(wrapper.find('[data-testid="ui-drawer"]').exists()).toBe(true))
  })

  it('keeps the page inside main, between the header and the footer', async () => {
    const wrapper = await mountClassicLayout()

    expect(wrapper.find('main [data-testid="page"]').exists()).toBe(true)
    const html = wrapper.html()
    expect(html.indexOf('data-testid="layout-header"')).toBeLessThan(html.indexOf('data-testid="page"'))
    expect(html.indexOf('data-testid="page"')).toBeLessThan(html.indexOf('data-testid="layout-footer"'))
  })

  it('fills the viewport height and paints the page surface', async () => {
    const wrapper = await mountClassicLayout()

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['min-h-dvh', 'bg-page']))
  })

  it('stamps the active template on the html element', async () => {
    await mountClassicLayout()

    const input = documentHead<{ htmlAttrs: { 'data-template': unknown } }>(useHead)
    expect(unref(input.htmlAttrs['data-template'])).toBe(TemplateName.Classic)
  })

  it('takes the header and the footer from the template the operator chose', async () => {
    const wrapper = await mountLayout({ settings: { [PortalSettingKey.Template]: TemplateName.Nova } })

    expect(wrapper.find('[data-testid="nova-skip-link"]').exists()).toBe(true)
    expect(wrapper.find('footer.tpl-nova').exists()).toBe(true)
    expect(wrapper.find('[data-testid="layout-header"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="layout-footer"]').exists()).toBe(false)
    expect(wrapper.classes()).toContain(`tpl-${TemplateName.Nova}`)
  })

  it('scopes the layout root with the active template class', async () => {
    const wrapper = await mountClassicLayout()

    expect(wrapper.classes()).toContain(`tpl-${TemplateName.Classic}`)
  })
})
