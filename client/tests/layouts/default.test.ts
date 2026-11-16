/**
 * Tests for the default layout: the active template's header and footer around the page slot,
 * the demo notice and the sign-in prompt, the floating actions and the cart drawer, and the template stamp on the document.
 *
 * The real template composable resolves the classic template's header and footer, which run with the real
 * catalogue, auth and cart stores against a stub server playing the BFF; only the head, the demo notice,
 * the sign-in prompt, the cart drawer, the floating actions, the cookie banner, the pickers and the domain search are stood in for.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { defineAsyncComponent, defineComponent, h, unref } from 'vue'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { CatalogPath } from '~/tests/helpers/catalogPaths'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import { registerPageComponents } from '~/tests/helpers/publicPage'
import { readOf, requestsTo } from '~/tests/helpers/stubRoutes'
import { waitUntil } from '~/tests/helpers/waitUntil'
import DefaultLayout from '~/layouts/default.vue'
import LayoutFooter from '~/components/layout/Footer.vue'
import LayoutHeader from '~/components/layout/Header.vue'
import { useAppColorMode } from '~/composables/useAppColorMode'
import { useAuthMode } from '~/composables/useAuthMode'
import { useBrandLogo } from '~/composables/useBrandLogo'
import { useContactDetails } from '~/composables/useContactDetails'
import { useLocalizedSetting } from '~/composables/useLocalizedSetting'
import { useSiteIdentity } from '~/composables/useSiteIdentity'
import { useTemplate } from '~/composables/useTemplate'
import { useCatalogStore } from '~/stores/catalog'
import { TemplateName } from '~/types/templatename'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies, stub } = usePortalSettingsServer({ useAppColorMode, useAuthMode, useBrandLogo, useContactDetails, useLocalizedSetting, useSiteIdentity, useTemplate })

/**
 * Builds a component that renders a marked element.
 *
 * @param testid - The `data-testid` the element carries.
 * @returns The component.
 */
const marker = (testid: string) => defineComponent({ setup: () => () => h('div', { 'data-testid': testid }) })

/** Spy for `useHead`, to read what the layout stamps on the document. */
const useHead = vi.fn()

describe('layouts/default', () => {
  beforeEach(() => {
    useHead.mockReset()
    vi.stubGlobal('useHead', useHead)
    vi.stubGlobal('defineAsyncComponent', defineAsyncComponent)
    replies[readOf(CatalogPath.ProductGroups)] = { body: [] }
    replies[readOf(CatalogPath.Products)] = { body: [] }
    registerPageComponents({ LayoutCartDrawer: marker('cart-drawer'), LayoutDemoBanner: marker('demo-banner'), LayoutSignInPrompt: marker('signin-prompt') })
  })

  /**
   * Mounts the layout with a page in its slot and waits for the template's header and footer to load.
   *
   * @returns The wrapper.
   */
  const mountLayout = async () => {
    await loadPortalSettings(replies)
    const wrapper = mountWithKit(DefaultLayout, {
      slots: { default: '<span data-testid="page">Page</span>' },
      components: { LayoutFooter, LayoutHeader },
      stubs: {
        UiFloatingActions: marker('floating-actions'),
        UiCookieBanner: marker('cookie-banner'),
        UiLanguageSwitcher: marker('language-switcher'),
        UiCurrencySwitcher: marker('currency-switcher'),
        SectionsDomainSearch: marker('domain-search'),
      },
    })
    await waitUntil(() => {
      expect(wrapper.find('[data-testid="layout-header"]').exists()).toBe(true)
      expect(wrapper.find('[data-testid="layout-footer"]').exists()).toBe(true)
    })
    // The header and the footer read the catalogue on mount; let it settle so the stub is not closed under a read.
    await waitUntil(() => expect(requestsTo(stub().requests, CatalogPath.ProductGroups).length).toBeGreaterThan(0))
    await waitUntil(() => expect(useCatalogStore().loading).toBe(false))
    return wrapper
  }

  it('renders the template header, the page, the template footer, the floating actions and the cart', async () => {
    const wrapper = await mountLayout()

    for (const id of ['demo-banner', 'signin-prompt', 'layout-header', 'page', 'layout-footer', 'floating-actions', 'cookie-banner', 'cart-drawer']) {
      expect(wrapper.find(`[data-testid="${id}"]`).exists()).toBe(true)
    }
  })

  it('keeps the page inside main, between the header and the footer', async () => {
    const wrapper = await mountLayout()

    expect(wrapper.find('main [data-testid="page"]').exists()).toBe(true)
    const html = wrapper.html()
    expect(html.indexOf('data-testid="layout-header"')).toBeLessThan(html.indexOf('data-testid="page"'))
    expect(html.indexOf('data-testid="page"')).toBeLessThan(html.indexOf('data-testid="layout-footer"'))
  })

  it('fills the viewport height and paints the page surface', async () => {
    const wrapper = await mountLayout()

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['min-h-dvh', 'bg-page']))
  })

  it('stamps the active template on the html element', async () => {
    await mountLayout()

    const input = useHead.mock.calls[0]![0] as { htmlAttrs: { 'data-template': unknown } }
    expect(unref(input.htmlAttrs['data-template'])).toBe(TemplateName.Classic)
  })

  it('scopes the layout root with the active template class', async () => {
    const wrapper = await mountLayout()

    expect(wrapper.classes()).toContain(`tpl-${TemplateName.Classic}`)
  })
})
