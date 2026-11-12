/**
 * Unit tests for the bundle page, against the real stores, API composables and transport with a stub BFF: it shows the bundle named by the address with a cycle choice per
 * component, asks for the domain when the bundle includes one, estimates the discount, and on
 * Continue fills the cart through the cart store and moves on to checkout.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { mountPage, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import { CatalogPath } from '~/tests/helpers/catalogPaths'
import { USD_CURRENCY, awaitCatalogue } from '~/tests/helpers/payerCatalogue'
import { readOf, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import BundlePage from '~/pages/bundles/[id].vue'
import BundleConfigure from '~/components/bundle/BundleConfigure.vue'
import BundleDomainPicker from '~/components/bundle/BundleDomainPicker.vue'
import CheckoutBundleSummary from '~/components/checkout/BundleSummary.vue'
import { useCartStore } from '~/stores/cart'
import { BundleDiscountType } from '~/types/bundlediscounttype'
import type { CatalogBundle } from '~/types/catalogbundle'
import { CartItemType } from '~/types/cartitemtype'
import { DomainOrderAction } from '~/types/domainorderaction'
import { PricingCycle } from '~/types/pricingcycle'
import type { TldPricing } from '~/types/tldpricing'

/**
 * Builds a bundle of one hosting plan.
 *
 * @param overrides Fields that differ from the defaults.
 * @returns The bundle.
 */
const bundle = (overrides: Partial<CatalogBundle> = {}): CatalogBundle => ({
  id: 4,
  name: 'Starter pack',
  description: null,
  includesDomain: false,
  domainYears: null,
  discountType: BundleDiscountType.Percentage,
  discountAmount: 10,
  discountCurrency: null,
  components: [{ productId: 11, name: 'Starter', isPrimary: true }],
  ...overrides,
})

/** The hosting plan and the domain product of the catalogue. */
const catalogue = [
  { id: 11, groupId: 1, name: 'Starter', pricing: { monthly: 10, annual: 100 } },
  { id: 7, groupId: 2, name: 'Domain Registration' },
]

/** A price table with one extension: ten a year to register, twelve to transfer. */
const pricing: TldPricing = {
  currency: { code: 'USD', prefix: '$' },
  pricing: [{ tld: '.com', sellCurrency: 'USD', register: [{ years: 1, amount: 10 }], transfer: [{ years: 1, amount: 12 }], categories: [] }],
}

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/** The `navigateTo` spy of the current test. */
let navigate: ReturnType<typeof vi.fn>

/**
 * Mounts the page for one bundle id.
 *
 * @param id The `id` route parameter.
 * @returns The wrapper.
 */
const mountBundle = async (id = '4') => {
  stubPublicPageRuntime({ params: { id } })
  const wrapper = await mountPage(BundlePage, { components: { BundleConfigure, BundleDomainPicker, CheckoutBundleSummary } })
  await awaitCatalogue()
  return wrapper
}

describe('bundle page', () => {
  beforeEach(async () => {
    navigate = vi.fn()
    vi.stubGlobal('navigateTo', navigate)
    table = {
      [readOf(CatalogPath.BillingCurrencies)]: { body: [USD_CURRENCY] },
      [readOf(CatalogPath.Products)]: { body: catalogue },
      [readOf(CatalogPath.ProductBundles)]: { body: [bundle()] },
      [readOf(CatalogPath.TldPricing)]: { body: pricing },
    }
    stub = await startTransport(routeTable(table))
    window.localStorage.clear()
  })

  afterEach(async () => {
    await stub.close()
  })

  it('shows the bundle, a cycle choice for its component and the estimated total after the discount', async () => {
    const wrapper = await mountBundle()

    expect(wrapper.find('[data-testid="bundle-name"]').text()).toBe('Starter pack')
    expect(wrapper.find('[data-testid="bundle-component-11"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="bundle-total"]').text()).toContain('9.00')
    expect(wrapper.find('[data-testid="bundle-summary-discount"]').text()).toContain('1.00')
  })

  it('tells a missing bundle from a found one', async () => {
    const wrapper = await mountBundle('99')

    expect(wrapper.find('[data-testid="bundle-missing"]').exists()).toBe(true)
  })

  it('puts the bundle in the cart on the chosen cycle, carrying its id, and moves on to checkout', async () => {
    const wrapper = await mountBundle()
    wrapper.findComponent(BundleConfigure).vm.$emit('update:cycle', 11, PricingCycle.Annual)
    await flushPromises()

    wrapper.findComponent(BundleConfigure).vm.$emit('continue')
    await flushPromises()

    const cart = useCartStore()
    expect(cart.bundle).toMatchObject({ id: 4, name: 'Starter pack', discountType: BundleDiscountType.Percentage })
    expect(cart.items).toHaveLength(1)
    expect(cart.items[0]).toMatchObject({ pid: 11, billingcycle: PricingCycle.Annual, amount: 100, currency: 'USD', bundleId: 4 })
    expect(navigate).toHaveBeenCalledWith('/en/checkout')
  })

  it('needs a domain before a bundle that includes one can continue', async () => {
    table[readOf(CatalogPath.ProductBundles)] = { body: [bundle({ includesDomain: true, domainYears: 2 })] }
    const wrapper = await mountBundle()

    expect(wrapper.find('[data-testid="bundle-domain-years"]').exists()).toBe(true)
    wrapper.findComponent(BundleConfigure).vm.$emit('continue')
    await flushPromises()

    expect(useCartStore().items).toEqual([])
    expect(navigate).not.toHaveBeenCalled()
  })

  it('adds the registration as a line for the bundle years, priced for all of them', async () => {
    table[readOf(CatalogPath.ProductBundles)] = { body: [bundle({ includesDomain: true, domainYears: 2 })] }
    const wrapper = await mountBundle()

    wrapper.findComponent(BundleDomainPicker).vm.$emit('select', { action: DomainOrderAction.Register, name: 'example.com', eppCode: '' })
    await flushPromises()
    wrapper.findComponent(BundleConfigure).vm.$emit('continue')
    await flushPromises()

    const items = useCartStore().items
    expect(items.map(i => i.pid)).toEqual([11, 7])
    expect(items[0]).toMatchObject({ domain: 'example.com', bundleId: 4 })
    expect(items[1]).toMatchObject({
      itemType: CartItemType.Domain, domain: 'example.com', domainAction: DomainOrderAction.Register, years: 2, amount: 20, tld: 'com', bundleId: 4,
    })
  })

  it('keeps the authorization code of a transfer, priced at the transfer rate', async () => {
    table[readOf(CatalogPath.ProductBundles)] = { body: [bundle({ includesDomain: true, domainYears: 1 })] }
    const wrapper = await mountBundle()

    wrapper.findComponent(BundleDomainPicker).vm.$emit('select', { action: DomainOrderAction.Transfer, name: 'example.com', eppCode: 'abc' })
    await flushPromises()
    wrapper.findComponent(BundleConfigure).vm.$emit('continue')
    await flushPromises()

    expect(useCartStore().items[1]).toMatchObject({ domainAction: DomainOrderAction.Transfer, eppCode: 'abc', years: 1, amount: 12 })
  })

  it('refuses a domain whose extension is not priced', async () => {
    table[readOf(CatalogPath.ProductBundles)] = { body: [bundle({ includesDomain: true, domainYears: 1 })] }
    const wrapper = await mountBundle()

    wrapper.findComponent(BundleDomainPicker).vm.$emit('select', { action: DomainOrderAction.Register, name: 'example.org', eppCode: '' })
    await flushPromises()

    expect(wrapper.find('[data-testid="bundle-domain-error"]').text()).toBe('domains.tldNotSupported')
    expect(wrapper.find('[data-testid="bundle-continue"]').attributes('disabled')).toBeDefined()
  })
})
