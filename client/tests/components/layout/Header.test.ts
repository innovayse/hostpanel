/**
 * Unit tests for the classic LayoutHeader: the logo lockup, the catalogue-driven navigation, the
 * shared controls, the signed-out and signed-in states, the drawer and the scroll shadow.
 *
 * The real catalogue and auth stores, API composables, transport and the operator's settings composables run
 * against a stub server playing the BFF; the language and currency pickers and the theme toggle run for real too.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import { CLIENT_LOGIN_PATH } from '~/constants/clientRoutes'
import LayoutHeader from '~/components/layout/Header.vue'
import { useAppColorMode } from '~/composables/useAppColorMode'
import { useAuthMode } from '~/composables/useAuthMode'
import { useBrandLogo } from '~/composables/useBrandLogo'
import { useSiteIdentity } from '~/composables/useSiteIdentity'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { useAuthStore } from '~/stores/auth'
import { useCatalogStore } from '~/stores/catalog'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { CatalogPath } from '~/tests/helpers/catalogPaths'
import { AMD_CURRENCY, USD_CURRENCY, awaitCatalogue } from '~/tests/helpers/payerCatalogue'
import { stubSwitchLocalePath } from '~/tests/helpers/runtimeStubs'
import { readOf, requestsTo } from '~/tests/helpers/stubRoutes'
import type { StubReply } from '~/tests/helpers/stubServer'
import type { PortalProduct } from '~/types/portalproduct'
import type { PortalProductGroup } from '~/types/portalproductgroup'
import { AuthMode } from '~/types/authmode'
import { ProductType } from '~/types/producttype'
import { waitUntil } from '~/tests/helpers/waitUntil'
import { SITE_DOMAINS_PATH, SITE_HOSTING_PATH, siteProductFamilyPath } from '~/constants/siteRoutes'

/** The page of the Cloud servers group, which the header builds from the group's name. */
const CLOUD_SERVERS_PAGE = siteProductFamilyPath('cloud-servers')

/** Groups the stubbed catalogue API returns: hosting and a VPS group. */
const GROUPS: PortalProductGroup[] = [
  { id: 1, name: 'Web hosting', productCount: 2 },
  { id: 9, name: 'Cloud servers', productCount: 1 },
]

/** Products the stubbed catalogue API returns. */
const PRODUCTS = [
  { id: 10, groupId: 1, type: ProductType.SharedHosting },
  { id: 90, groupId: 9, type: ProductType.Vps },
] as PortalProduct[]

/** What the stub answers, by route, and the accessor of the stub the hooks start and close around each test. */
const { replies, stub } = usePortalSettingsServer({ useAppColorMode, useAuthMode, useBrandLogo, useSiteIdentity })

/** The settings of an operator who has named the site and uploaded no logo. */
const NAMED_SITE: Record<string, string> = { [PortalSettingKey.SiteName]: 'Acme Host' }

/** What the stub answers the product groups read with; a test changes it before it mounts. */
let groupsReply: StubReply = { body: GROUPS }

/** A signed-in client. */
const client = makeClientUser()

/**
 * Mounts the header with its real pickers and waits until the catalogue and the currencies have been read.
 *
 * @param settings The operator's settings by their dotted key.
 * @param rereadForPayer Whether to read the families again once the payer's currency is known, as a page does.
 * @returns The wrapper.
 */
const mountHeader = async (settings: Record<string, string> = NAMED_SITE, rereadForPayer = true) => {
  await loadPortalSettings(replies, settings)
  const wrapper = mountWithKit(LayoutHeader, { attachToBody: true })
  await waitUntil(() => expect(requestsTo(stub().requests, CatalogPath.ProductGroups).length).toBeGreaterThan(0))
  await awaitCatalogue()
  // The header reads the families once, before the payer's currency is known. A page reads them again when the
  // currency resolves (it watches the payer's code), which the real currency switcher's load sets off here.
  if (rereadForPayer) {
    await useCatalogStore().loadFamilies()
  }
  return wrapper
}

/**
 * The test ids of the desktop navigation's direct links.
 *
 * @param wrapper - The mounted header.
 * @returns Their `data-testid` attributes in order.
 */
const desktopLinkKeys = (wrapper: Awaited<ReturnType<typeof mountHeader>>): (string | undefined)[] =>
  wrapper.find('[data-testid="layout-desktop-nav"]').findAll('a').map((link) => link.attributes('data-testid'))

describe('LayoutHeader', () => {
  beforeEach(() => {
    vi.stubGlobal('useRuntimeConfig', () => ({ public: { authMode: AuthMode.Local } }))
    groupsReply = { body: GROUPS }
    replies[readOf(CatalogPath.ProductGroups)] = () => groupsReply
    replies[readOf(CatalogPath.Products)] = { body: PRODUCTS }
    replies[readOf(CatalogPath.BillingCurrencies)] = { body: [USD_CURRENCY, AMD_CURRENCY] }
    stubSwitchLocalePath()
    window.localStorage.clear()
  })

  afterEach(() => {
    document.body.classList.remove('overflow-hidden')
  })

  it('draws the mark and the site name beside it, linking home', async () => {
    const wrapper = await mountHeader()

    const logo = wrapper.find('[data-testid="layout-logo"]')
    expect(logo.attributes('href')).toBe('/')
    expect(logo.text()).toBe('Acme Host')
    expect(logo.find('[data-testid="app-logo-mark"]').exists()).toBe(true)
  })

  it('draws the uploaded logo instead of the mark', async () => {
    const wrapper = await mountHeader({ ...NAMED_SITE, [PortalSettingKey.Logo]: '/api/uploads/logo.png' })

    const logo = wrapper.find('[data-testid="layout-logo"]')
    expect(logo.find('[data-testid="app-logo-light"]').attributes('src')).toBe('/api/uploads/logo.png')
    expect(logo.find('[data-testid="app-logo-mark"]').exists()).toBe(false)
  })

  it('shows Hosting and Support as dropdowns, with VPS inside Hosting and no top-level VPS entry', async () => {
    const wrapper = await mountHeader()

    expect(wrapper.find('[data-testid="header-nav-hosting-trigger"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="header-nav-support-trigger"]').exists()).toBe(true)
    expect(desktopLinkKeys(wrapper)).toEqual(['header-nav-domains', 'header-nav-products'])
    expect(wrapper.find('[data-testid="header-nav-vps"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="header-nav-servers"]').exists()).toBe(false)

    await wrapper.find('[data-testid="header-nav-hosting-trigger"]').trigger('click')

    expect(wrapper.find('[data-testid="header-nav-child-group-9"]').attributes('href')).toBe(CLOUD_SERVERS_PAGE)
  })

  it('keeps Hosting a plain link when the catalogue read fails', async () => {
    groupsReply = { status: 503, body: { type: 'UNAVAILABLE', detail: 'Offline.' } }
    const wrapper = await mountHeader()

    expect(desktopLinkKeys(wrapper)).toEqual(['header-nav-hosting', 'header-nav-domains', 'header-nav-products'])
  })

  it('marks the current page in the bar', async () => {
    vi.stubGlobal('useRoute', () => ({ params: {}, query: {}, path: SITE_DOMAINS_PATH, fullPath: SITE_DOMAINS_PATH, hash: '', meta: {} }))
    const wrapper = await mountHeader()

    expect(wrapper.find('[data-testid="header-nav-domains"]').attributes('aria-current')).toBe('page')
    expect(wrapper.find('[data-testid="header-nav-products"]').attributes('aria-current')).toBeUndefined()
  })

  it('marks only Hosting, not Products, on a page of a Hosting dropdown group under /products', async () => {
    vi.stubGlobal('useRoute', () => ({ params: {}, query: {}, path: CLOUD_SERVERS_PAGE, fullPath: CLOUD_SERVERS_PAGE, hash: '', meta: {} }))
    const wrapper = await mountHeader()

    expect(wrapper.find('[data-testid="header-nav-hosting-trigger"]').classes()).toContain('text-classic-on-brand')
    expect(wrapper.find('[data-testid="header-nav-products"]').attributes('aria-current')).toBeUndefined()
    expect(wrapper.find('[data-testid="header-nav-products"]').classes()).not.toContain('text-classic-on-brand')
  })

  it('offers the light/dark switch and the cart in both the desktop and the small-screen bar', async () => {
    const wrapper = await mountHeader()

    expect(wrapper.findAll('[data-testid="theme-toggle"]')).toHaveLength(2)
    expect(wrapper.findAll('[data-testid="layout-cart-button"]')).toHaveLength(2)
  })

  it('gives the theme toggle, cart and pickers the same control frame', async () => {
    const wrapper = await mountHeader()

    for (const testid of ['theme-toggle', 'layout-cart-button']) {
      const classes = wrapper.find(`[data-testid="${testid}"]`).classes()
      expect(classes).toContain('h-10')
      expect(classes).toContain('rounded-lg')
      expect(classes).toContain('border-classic-border-strong')
    }
    expect(wrapper.find('[data-testid="language-switcher"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="currency-switcher"]').exists()).toBe(true)
  })

  it('offers sign in and get started to a visitor', async () => {
    const wrapper = await mountHeader()

    expect(wrapper.find('[data-testid="layout-sign-in"]').attributes('href')).toBe(CLIENT_LOGIN_PATH)
    expect(wrapper.find('[data-testid="layout-get-started"]').attributes('href')).toBe(SITE_HOSTING_PATH)
    expect(wrapper.find('[data-testid="header-account"]').exists()).toBe(false)
  })

  it('swaps them for the account menu when a client is signed in', async () => {
    const wrapper = await mountHeader()
    useAuthStore().user = client
    await nextTick()

    expect(wrapper.find('[data-testid="header-account"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="layout-sign-in"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="layout-get-started"]').exists()).toBe(false)
  })

  it('keeps the drawer closed until the menu button is pressed', async () => {
    const wrapper = await mountHeader()
    const toggle = wrapper.find('[data-testid="layout-mobile-toggle"]')
    expect(wrapper.find('[data-testid="header-drawer-nav"]').exists()).toBe(false)
    expect(toggle.attributes('aria-expanded')).toBe('false')

    await toggle.trigger('click')

    expect(wrapper.find('[data-testid="header-drawer-nav"]').exists()).toBe(true)
    expect(toggle.attributes('aria-expanded')).toBe('true')
  })

  it('holds every navigation entry and the sign-in pair in the drawer', async () => {
    const wrapper = await mountHeader()
    await wrapper.find('[data-testid="layout-mobile-toggle"]').trigger('click')

    const drawer = wrapper.find('[data-testid="header-drawer-nav"]')
    expect(drawer.find('[data-testid="header-drawer-hosting"]').exists()).toBe(true)
    expect(drawer.find('[data-testid="header-drawer-child-group-9"]').attributes('href')).toBe(CLOUD_SERVERS_PAGE)
    expect(drawer.find('[data-testid="header-drawer-child-faq"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="header-drawer-sign-in"]').exists()).toBe(true)
  })

  it('loads the catalogue families once, during setup, not after mount', async () => {
    await mountHeader(NAMED_SITE, false)

    expect(requestsTo(stub().requests, CatalogPath.ProductGroups)).toHaveLength(1)
  })

  it('renders the widget mounts instead of the local account part when the widget is served', async () => {
    // The header imports the composable directly, so the deployment is described through the runtime config it reads.
    vi.stubGlobal('useRuntimeConfig', () => ({ public: { authMode: AuthMode.Sso, mainUrl: 'https://portal.example.com' } }))
    const wrapper = await mountHeader()

    expect(wrapper.find('#inno-launcher-mount').exists()).toBe(true)
    expect(wrapper.find('#inno-account-mount').exists()).toBe(true)
    expect(wrapper.find('[data-testid="layout-sign-in"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="header-account"]').exists()).toBe(false)
  })

  it('does not render the widget mounts on a deployment without it', async () => {
    const wrapper = await mountHeader()

    expect(wrapper.find('#inno-launcher-mount').exists()).toBe(false)
    expect(wrapper.find('#inno-account-mount').exists()).toBe(false)
  })

  it('reinitialises the widget after mount when it is present', async () => {
    const reinit = vi.fn()
    vi.stubGlobal('InnoWidget', { reinit })
    await mountHeader()

    expect(reinit).toHaveBeenCalledTimes(1)
    vi.stubGlobal('InnoWidget', undefined)
  })

  it('gets a shadow once the page has scrolled', async () => {
    const wrapper = await mountHeader()
    const header = wrapper.find('[data-testid="layout-header"]')
    expect(header.classes()).not.toContain('shadow-sm')

    Object.defineProperty(window, 'scrollY', { value: 120, configurable: true })
    window.dispatchEvent(new Event('scroll'))
    await nextTick()

    expect(header.classes()).toContain('shadow-sm')
    Object.defineProperty(window, 'scrollY', { value: 0, configurable: true })
  })
})
