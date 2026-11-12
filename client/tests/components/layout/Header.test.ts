/**
 * Unit tests for the classic LayoutHeader: the logo lockup, the catalogue-driven navigation, the
 * shared controls, the signed-out and signed-in states, the drawer and the scroll shadow.
 *
 * The real catalogue and auth stores, API composables and transport run against a stub server
 * playing the BFF; only the two pickers are stood in for.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h, nextTick, ref } from 'vue'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { stubTemplateGlobals } from '~/tests/helpers/templateGlobals'
import LayoutHeader from '~/components/layout/Header.vue'
import { useAuthStore } from '~/stores/auth'
import { useCatalogStore } from '~/stores/catalog'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubReply, StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import type { PortalProduct } from '~/types/portalproduct'
import type { PortalProductGroup } from '~/types/portalproductgroup'
import { ProductType } from '~/types/producttype'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** Path of the endpoint that lists the product groups. */
const GROUPS_PATH = '/api/portal/public/product-groups'

/** Path of the endpoint that lists the products. */
const PRODUCTS_PATH = '/api/portal/public/products'

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

/** The stub playing the BFF. */
let stub: StubServer

/** What the stub answers the product groups read with; a test changes it before it mounts. */
let groupsReply: StubReply = { body: GROUPS }

/** Stand-in for the language switcher, which needs the i18n runtime. */
const LanguageSwitcherStub = defineComponent({
  name: 'UiLanguageSwitcher',
  setup: () => () => h('div', { 'data-testid': 'language-switcher-stub' }),
})

/** Stand-in for the currency switcher, which has its own test. */
const CurrencySwitcherStub = defineComponent({
  name: 'UiCurrencySwitcher',
  setup: () => () => h('div', { 'data-testid': 'currency-switcher-stub' }),
})

/** A signed-in client. */
const client = makeClientUser()

/**
 * Mounts the header with the pickers stood in for and waits until the catalogue read has settled.
 *
 * @returns The wrapper.
 */
const mountHeader = async () => {
  const wrapper = mountWithKit(LayoutHeader, {
    components: { UiLanguageSwitcher: LanguageSwitcherStub, UiCurrencySwitcher: CurrencySwitcherStub },
    attachToBody: true,
  })
  await waitUntil(() => expect(requestsTo(stub.requests, GROUPS_PATH).length).toBeGreaterThan(0))
  await waitUntil(() => expect(useCatalogStore().loading).toBe(false))
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
  beforeEach(async () => {
    stubTemplateGlobals()
    vi.stubGlobal('useRuntimeConfig', () => ({ public: {} }))
    groupsReply = { body: GROUPS }
    stub = await startTransport(routeTable({
      [`GET ${GROUPS_PATH}`]: () => groupsReply,
      [`GET ${PRODUCTS_PATH}`]: { body: PRODUCTS },
    }))
    window.localStorage.clear()
  })

  afterEach(async () => {
    await stub.close()
    // Put back the setup's empty route, which the active-state tests replace.
    vi.stubGlobal('useRoute', () => ({ params: {}, query: {}, path: '/', fullPath: '/', meta: {} }))
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
    stubTemplateGlobals({ logo: '/api/uploads/logo.png' })
    const wrapper = await mountHeader()

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

    expect(wrapper.find('[data-testid="header-nav-child-group-9"]').attributes('href')).toBe('/products/cloud-servers')
  })

  it('keeps Hosting a plain link when the catalogue read fails', async () => {
    groupsReply = { status: 503, body: { type: 'UNAVAILABLE', detail: 'Offline.' } }
    const wrapper = await mountHeader()

    expect(desktopLinkKeys(wrapper)).toEqual(['header-nav-hosting', 'header-nav-domains', 'header-nav-products'])
  })

  it('marks the current page in the bar', async () => {
    vi.stubGlobal('useRoute', () => ({ params: {}, query: {}, path: '/domains', fullPath: '/domains', hash: '', meta: {} }))
    const wrapper = await mountHeader()

    expect(wrapper.find('[data-testid="header-nav-domains"]').attributes('aria-current')).toBe('page')
    expect(wrapper.find('[data-testid="header-nav-products"]').attributes('aria-current')).toBeUndefined()
  })

  it('marks only Hosting, not Products, on a page of a Hosting dropdown group under /products', async () => {
    vi.stubGlobal('useRoute', () => ({ params: {}, query: {}, path: '/products/cloud-servers', fullPath: '/products/cloud-servers', hash: '', meta: {} }))
    const wrapper = await mountHeader()

    expect(wrapper.find('[data-testid="header-nav-hosting-trigger"]').classes()).toContain('text-classic-on-brand')
    expect(wrapper.find('[data-testid="header-nav-products"]').attributes('aria-current')).toBeUndefined()
    expect(wrapper.find('[data-testid="header-nav-products"]').classes()).not.toContain('text-classic-on-brand')
  })

  it('offers the light/dark switch and the cart in both the desktop and the small-screen bar', async () => {
    // The shared stub only knows `isDark`; the switch also reads the operator's toggle setting.
    vi.stubGlobal('useAppColorMode', () => ({ isDark: ref(false), applied: ref('light'), userToggleEnabled: ref(true), toggle: vi.fn() }))
    const wrapper = await mountHeader()

    expect(wrapper.findAll('[data-testid="theme-toggle"]')).toHaveLength(2)
    expect(wrapper.findAll('[data-testid="layout-cart-button"]')).toHaveLength(2)
  })

  it('gives the theme toggle, cart and pickers the same control frame', async () => {
    vi.stubGlobal('useAppColorMode', () => ({ isDark: ref(false), applied: ref('light'), userToggleEnabled: ref(true), toggle: vi.fn() }))
    const wrapper = await mountHeader()

    for (const testid of ['theme-toggle', 'layout-cart-button']) {
      const classes = wrapper.find(`[data-testid="${testid}"]`).classes()
      expect(classes).toContain('h-10')
      expect(classes).toContain('rounded-lg')
      expect(classes).toContain('border-classic-border-strong')
    }
    expect(wrapper.find('[data-testid="language-switcher-stub"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="currency-switcher-stub"]').exists()).toBe(true)
  })

  it('offers sign in and get started to a visitor', async () => {
    const wrapper = await mountHeader()

    expect(wrapper.find('[data-testid="layout-sign-in"]').attributes('href')).toBe('/client/login')
    expect(wrapper.find('[data-testid="layout-get-started"]').attributes('href')).toBe('/hosting')
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
    expect(drawer.find('[data-testid="header-drawer-child-group-9"]').attributes('href')).toBe('/products/cloud-servers')
    expect(drawer.find('[data-testid="header-drawer-child-faq"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="header-drawer-sign-in"]').exists()).toBe(true)
  })

  it('loads the catalogue families once, during setup, not after mount', async () => {
    await mountHeader()

    expect(requestsTo(stub.requests, GROUPS_PATH)).toHaveLength(1)
  })

  it('renders the widget mounts instead of the local account part when the widget is served', async () => {
    // The header imports the composable directly, so the deployment is described through the runtime config it reads.
    vi.stubGlobal('useRuntimeConfig', () => ({ public: { authMode: 'sso', mainUrl: 'https://portal.example.com' } }))
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
