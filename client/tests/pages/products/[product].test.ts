/**
 * Unit tests for the product detail page against the real catalogue, currency and cart stores, the
 * real API composables and the real transport, with a stub BFF answering on a real socket: it finds
 * the family named by the address and names the page after the family, shows its plans (description,
 * features, price) in the payer's currency, adds a plan to the cart in the chosen cycle, links the
 * external order page only when the deployment has one, compares the plans when their specifications
 * allow it, publishes the family as structured data, and answers a missing product with a notice.
 *
 * @module tests/pages/products/[product].test
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountPage, publishedSchemas, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import { CatalogPath } from '~/tests/helpers/catalogPaths'
import { CLASSIC_COMPONENTS } from '~/tests/helpers/classicKit'
import { faqFixture } from '~/tests/helpers/storefrontFixtures'
import { StorefrontPath } from '~/tests/helpers/storefrontSection'
import { USD_CURRENCY, awaitCatalogue } from '~/tests/helpers/payerCatalogue'
import { queryOf, requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { waitUntil } from '~/tests/helpers/waitUntil'
import ProductPage from '~/pages/products/[product].vue'
import CatalogProductDetail from '~/components/catalog/ProductDetail.vue'
import CatalogProductPlanCard from '~/components/catalog/ProductPlanCard.vue'
import CatalogProductPlansTable from '~/components/catalog/ProductPlansTable.vue'
import CatalogProductSectionHeader from '~/components/catalog/ProductSectionHeader.vue'
import SectionsFAQ from '~/components/sections/SectionsFAQ.vue'
import { useTemplate } from '~/composables/useTemplate'
import { useCartStore } from '~/stores/cart'
import type { PortalProduct } from '~/types/portalproduct'
import type { PortalProductGroup } from '~/types/portalproductgroup'
import type { ProductFeature } from '~/types/productfeature'
import { ProductType } from '~/types/producttype'

/** The family's group. */
const groups: PortalProductGroup[] = [{ id: 3, name: 'Alpha Suite', description: 'The family description', productCount: 3 }]

/** A family with a parent and two plans. */
const catalogue: PortalProduct[] = [
  { id: 10, groupId: 3, name: 'Alpha Suite', slug: 'alpha-suite', description: 'Learn online<br />✔ Courses' },
  { id: 11, groupId: 3, name: 'Starter', slug: 'starter', description: 'For a first site<br />✔ One site', pricing: { monthly: 10, annual: 100 } },
  { id: 12, groupId: 3, name: 'Pro', slug: 'pro', description: '✔ Two sites', pricing: { monthly: 20, annual: null } },
]

/** Specification lines the two plans share, so the family is comparable. */
const specifications: ProductFeature[] = [
  { id: 1, productId: 11, label: 'Disk', value: '10 GB', sortOrder: 1 },
  { id: 2, productId: 12, label: 'Disk', value: '20 GB', sortOrder: 1 },
  { id: 3, productId: 11, label: 'Sites', value: '1', sortOrder: 2 },
  { id: 4, productId: 12, label: 'Sites', value: '2', sortOrder: 2 },
]

/** The deployment's public address, which every absolute SEO URL of the page starts with. */
const SITE_URL = 'https://panel.test'

/** The spies of the latest mount, so a test reads the SEO tags the page published. */
let spies: ReturnType<typeof stubPublicPageRuntime>

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/**
 * Mounts the page for one product address and waits until the catalogue has been read in the payer currency.
 *
 * @param product The `product` route parameter.
 * @param publicConfig Further public runtime config of the deployment.
 * @returns The wrapper.
 */
const mountProduct = async (product = 'alpha-suite', publicConfig: Record<string, unknown> = {}) => {
  spies = stubPublicPageRuntime({ params: { product }, baseUrl: SITE_URL, publicConfig })
  vi.stubGlobal('useTemplate', useTemplate)
  const wrapper = await mountPage(ProductPage, { components: { ...CLASSIC_COMPONENTS, CatalogProductDetail, CatalogProductPlanCard, CatalogProductPlansTable, CatalogProductSectionHeader, SectionsFAQ }, stubs: { 'i18n-t': true } })
  await awaitCatalogue()
  return wrapper
}

describe('product detail page', () => {
  beforeEach(async () => {
    window.localStorage.clear()
    vi.stubGlobal('open', vi.fn())
    table = {
      [`GET ${CatalogPath.BillingCurrencies}`]: { body: [USD_CURRENCY] },
      [`GET ${CatalogPath.ProductGroups}`]: { body: groups },
      [`GET ${CatalogPath.Products}`]: { body: catalogue },
      [`GET ${CatalogPath.ProductFeatures}`]: { body: [] },
      [`GET ${StorefrontPath.Faqs}`]: { body: [1, 2, 3, 4, 5, 6].map((id) => faqFixture(id)) },
    }
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('builds its absolute SEO URLs from the configured site URL', async () => {
    await mountProduct()

    const tags = spies.useSeoMeta.mock.calls.at(-1)?.[0] as { ogImage: string, ogUrl: string }
    expect(tags.ogImage).toBe(`${SITE_URL}/og-image.jpg`)
    expect(tags.ogUrl).toMatch(/^https:\/\/panel\.test\//)
  })

  it('shows the product named by the address with its plans and their monthly prices', async () => {
    const wrapper = await mountProduct()

    expect(wrapper.find('[data-testid="product-name"]').text()).toBe('Alpha Suite')
    expect(wrapper.find('[data-testid="product-plan-11"] [data-testid="product-plan-price"]').text()).toContain('10.00')
    expect(wrapper.find('[data-testid="product-plan-12"] [data-testid="product-plan-price"]').text()).toContain('20.00')
  })

  it('shows the first five questions of the shared FAQ under the plans', async () => {
    const wrapper = await mountProduct()

    await waitUntil(() => expect(wrapper.find('[data-testid="sections-faq"]').exists()).toBe(true))
    expect(wrapper.findAll('[data-testid^="ui-accordion-toggle-"]')).toHaveLength(5)
    expect(wrapper.find('[data-testid="sections-faq"]').text()).not.toContain('Example question 6?')
  })

  it('names the page after the family and shows the family description, not the first plan', async () => {
    const wrapper = await mountProduct('starter')

    expect(wrapper.find('[data-testid="product-name"]').text()).toBe('Alpha Suite')
    expect(wrapper.find('[data-testid="product-description"]').text()).toBe('The family description')
    const tags = spies.useSeoMeta.mock.calls.at(-1)?.[0] as { title: string }
    expect(tags.title).toContain('Alpha Suite')
  })

  it('shows each plan with its short description and its feature list from the catalogue', async () => {
    const wrapper = await mountProduct()
    const starter = wrapper.find('[data-testid="product-plan-11"]')

    expect(starter.find('[data-testid="product-plan-description"]').text()).toBe('For a first site')
    expect(starter.find('[data-testid="product-plan-features"]').text()).toContain('One site')
    expect(starter.find('[data-testid="product-plan-period"]').text()).toBe('products.pricing.period.monthly')
  })

  it('offers the annual cycle only when a plan is priced for it, and switches the prices to it', async () => {
    const wrapper = await mountProduct()

    await wrapper.find('[data-testid="product-cycle-annual"]').trigger('click')

    expect(wrapper.find('[data-testid="product-plan-11"] [data-testid="product-plan-price"]').text()).toContain('100.00')
    expect(wrapper.find('[data-testid="product-plan-12"] [data-testid="product-plan-price"]').text()).toBe('—')
    expect(wrapper.find('[data-testid="product-plan-11"] [data-testid="product-plan-period"]').text()).toBe('products.pricing.period.annually')
    expect(wrapper.find('[data-testid="product-plan-11"] [data-testid="product-plan-saving"]').exists()).toBe(true)
  })

  it('compares the plans when their specification lines overlap', async () => {
    table[`GET ${CatalogPath.ProductFeatures}`] = { body: specifications }
    const wrapper = await mountProduct()

    expect(requestsTo(stub.requests, CatalogPath.ProductFeatures).map(queryOf)).toContainEqual({ gid: '3' })
    expect(wrapper.find('[data-testid="product-compare"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="product-compare-row-Disk"]').text()).toContain('20 GB')
  })

  it('draws no comparison when the catalogue has no shared specification lines', async () => {
    const wrapper = await mountProduct()

    expect(wrapper.find('[data-testid="product-compare"]').exists()).toBe(false)
  })

  it('publishes the family as a Product with an Offer per priced plan', async () => {
    const wrapper = await mountProduct()
    expect(wrapper.exists()).toBe(true)

    const family = publishedSchemas(spies).find((document) => document['@type'] === 'Product')

    expect(family).toMatchObject({ name: 'Alpha Suite', url: `${SITE_URL}/products/alpha-suite` })
    expect(family?.offers).toEqual([
      { name: 'Starter', price: 10 },
      { name: 'Pro', price: 20 },
    ].map(({ name, price }) => ({
      '@type': 'Offer',
      name,
      url: `${SITE_URL}/products/alpha-suite`,
      price,
      priceCurrency: 'USD',
      priceSpecification: { '@type': 'UnitPriceSpecification', price, priceCurrency: 'USD', billingDuration: 'P1M' },
    })))
  })

  it('adds a plan to the cart with the selected cycle and the payer currency', async () => {
    const wrapper = await mountProduct()

    await wrapper.find('[data-testid="product-add-11"]').trigger('click')

    expect(useCartStore().items[0]).toMatchObject({ pid: 11, name: 'Starter', billingcycle: 'monthly', amount: 10, currency: 'USD' })
  })

  it('keeps the type of a digital plan on its cart line, and none for a plan without one', async () => {
    table[`GET ${CatalogPath.Products}`] = { body: catalogue.map((entry) => (entry.id === 11 ? { ...entry, type: ProductType.Digital } : entry)) }
    const wrapper = await mountProduct()

    await wrapper.find('[data-testid="product-add-11"]').trigger('click')
    await wrapper.find('[data-testid="product-add-12"]').trigger('click')

    const [digital, other] = useCartStore().items
    expect(digital?.productType).toBe(ProductType.Digital)
    expect(other?.productType).toBeUndefined()
  })

  it('marks a plan that is already in the cart', async () => {
    const wrapper = await mountProduct()
    await wrapper.find('[data-testid="product-add-11"]').trigger('click')

    expect(wrapper.find('[data-testid="product-add-11"]').text()).toContain('hosting.inCart')
  })

  it('has no external trial button when the deployment runs no billing portal', async () => {
    const wrapper = await mountProduct()

    expect(wrapper.find('[data-testid="product-trial-11"]').exists()).toBe(false)
  })

  it('opens the billing-portal order page in a new tab when the deployment has one', async () => {
    const wrapper = await mountProduct('alpha-suite', { billingPortalUrl: 'https://billing.test' })

    await wrapper.find('[data-testid="product-trial-11"]').trigger('click')

    expect(window.open).toHaveBeenCalledWith('https://billing.test/cart.php?a=add&pid=11&billingcycle=monthly', '_blank')
  })

  it('answers an unknown product with a notice and the way back', async () => {
    const wrapper = await mountProduct('no-such-product')

    expect(wrapper.find('[data-testid="product-not-found"]').text()).toContain('products.notFound')
    expect(wrapper.find('[data-testid="product-back"]').attributes('href')).toBe('/en/products')
    expect(wrapper.find('[data-testid="product-detail"]').exists()).toBe(false)
  })
})
