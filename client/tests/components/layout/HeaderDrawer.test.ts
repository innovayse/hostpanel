/**
 * Unit tests for LayoutHeaderDrawer: every navigation entry, the signed-out and signed-in
 * footers, the preference pickers and closing.
 *
 * The real auth store, API composable and transport run against a stub server playing the BFF;
 * only the two pickers are stood in for.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { stubTemplateGlobals } from '~/tests/helpers/templateGlobals'
import LayoutHeaderDrawer from '~/components/layout/HeaderDrawer.vue'
import { useAuthStore } from '~/stores/auth'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import type { HeaderNavItem } from '~/types/headernavitem'

/** Path of the endpoint that ends the session. */
const LOGOUT_PATH = '/api/portal/auth/logout'

/** The stub playing the BFF. */
let stub: StubServer

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

/** A plain link and a group. */
const items: HeaderNavItem[] = [
  { key: 'domains', label: 'Domains', to: '/en/domains' },
  {
    key: 'support',
    label: 'Support',
    children: [
      { key: 'help', label: 'Help center', icon: 'mdi:book-open-variant', to: '/en/knowledgebase' },
      { key: 'faq', label: 'FAQ', icon: 'mdi:frequently-asked-questions', to: '/en/faq' },
    ],
  },
]

/** A signed-in client. */
const client = makeClientUser()

/**
 * Mounts an open drawer.
 *
 * @param path - The current page path.
 * @returns The wrapper.
 */
const mountDrawer = (path = '/en/') => mountWithKit(LayoutHeaderDrawer, {
  props: { modelValue: true, items, path },
  components: { UiLanguageSwitcher: LanguageSwitcherStub, UiCurrencySwitcher: CurrencySwitcherStub },
  attachToBody: true,
})

describe('LayoutHeaderDrawer', () => {
  beforeEach(async () => {
    stubTemplateGlobals()
    stub = await startTransport(routeTable({ [`POST ${LOGOUT_PATH}`]: { body: {} } }))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('renders nothing while closed', () => {
    const wrapper = mountWithKit(LayoutHeaderDrawer, { props: { modelValue: false, items, path: '/' } })

    expect(wrapper.find('[data-testid="header-drawer-nav"]').exists()).toBe(false)
  })

  it('lists a plain link and every link of a group, group headed and open', () => {
    const wrapper = mountDrawer()

    expect(wrapper.find('[data-testid="header-drawer-domains"]').attributes('href')).toBe('/en/domains')
    expect(wrapper.find('[data-testid="header-drawer-support"]').text()).toContain('Support')
    expect(wrapper.find('[data-testid="header-drawer-child-help"]').attributes('href')).toBe('/en/knowledgebase')
    expect(wrapper.find('[data-testid="header-drawer-child-faq"]').attributes('href')).toBe('/en/faq')
  })

  it('marks the current page', () => {
    const wrapper = mountDrawer('/en/faq')

    expect(wrapper.find('[data-testid="header-drawer-child-faq"]').attributes('aria-current')).toBe('page')
    expect(wrapper.find('[data-testid="header-drawer-domains"]').attributes('aria-current')).toBeUndefined()
  })

  it('marks Products idle when the page belongs to a group link under /products', () => {
    const nested: HeaderNavItem[] = [
      { key: 'hosting', label: 'Hosting', children: [{ key: 'mail', label: 'Mail', icon: 'mdi:email', to: '/en/products/mail' }] },
      { key: 'products', label: 'Products', to: '/en/products' },
    ]
    const wrapper = mountWithKit(LayoutHeaderDrawer, {
      props: { modelValue: true, items: nested, path: '/en/products/mail' },
      components: { UiLanguageSwitcher: LanguageSwitcherStub, UiCurrencySwitcher: CurrencySwitcherStub },
      attachToBody: true,
    })

    expect(wrapper.find('[data-testid="header-drawer-child-mail"]').attributes('aria-current')).toBe('page')
    expect(wrapper.find('[data-testid="header-drawer-products"]').attributes('aria-current')).toBeUndefined()
  })

  it('draws the icon tile of every group link, inverted on the current page', () => {
    const wrapper = mountDrawer('/en/faq')

    expect(wrapper.find('[data-testid="header-drawer-child-help-icon"] [data-icon="mdi:book-open-variant"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="header-drawer-child-help-icon"]').classes()).toContain('bg-classic-brand-soft')
    expect(wrapper.find('[data-testid="header-drawer-child-faq-icon"]').classes()).toContain('bg-classic-on-brand/20')
  })

  it('carries the preference pickers', () => {
    const wrapper = mountDrawer()

    const preferences = wrapper.find('[data-testid="header-drawer-preferences"]')
    expect(preferences.find('[data-testid="language-switcher-stub"]').exists()).toBe(true)
    expect(preferences.find('[data-testid="currency-switcher-stub"]').exists()).toBe(true)
  })

  it('offers sign in and get started to a visitor', () => {
    const wrapper = mountDrawer()

    expect(wrapper.find('[data-testid="header-drawer-sign-in"]').attributes('href')).toBe('/client/login')
    expect(wrapper.find('[data-testid="header-drawer-get-started"]').attributes('href')).toBe('/hosting')
    expect(wrapper.find('[data-testid="header-drawer-account"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="header-drawer-sign-out"]').exists()).toBe(false)
  })

  it('shows the account links and sign-out to a signed-in client', async () => {
    const wrapper = mountDrawer()
    useAuthStore().user = client
    await wrapper.vm.$nextTick()

    const account = wrapper.find('[data-testid="header-drawer-account"]')
    expect(account.text()).toContain('Ada Lovelace')
    expect(wrapper.find('[data-testid="header-drawer-account-invoices"]').attributes('href')).toBe('/client/invoices')
    expect(wrapper.find('[data-testid="header-drawer-sign-out"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="header-drawer-sign-in"]').exists()).toBe(false)
  })

  it('asks to close when a link is chosen', async () => {
    const wrapper = mountDrawer()

    await wrapper.find('[data-testid="header-drawer-domains"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([false])
  })

  it('closes and signs out through the store', async () => {
    const wrapper = mountDrawer()
    const store = useAuthStore()
    store.user = client
    await wrapper.vm.$nextTick()

    await wrapper.find('[data-testid="header-drawer-sign-out"]').trigger('click')

    await vi.waitFor(() => expect(requestsTo(stub.requests, LOGOUT_PATH)).toHaveLength(1))
    await vi.waitFor(() => expect(store.user).toBeNull())
    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([false])
  })
})
