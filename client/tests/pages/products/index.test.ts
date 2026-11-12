/**
 * Unit tests for the products page against the real catalogue, currency and storefront stores, the
 * real API composables and the real transport, with a stub BFF answering on a real socket: it reads
 * the family groups in the payer's currency, builds the families with the domains card first, feeds
 * the catalogue and comparison blocks, and sets the head.
 *
 * @module tests/pages/products/index.test
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountPage, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import type { PublicPageSpies } from '~/tests/helpers/publicPage'
import { CatalogPath } from '~/tests/helpers/catalogPaths'
import { USD_CURRENCY, awaitCatalogue } from '~/tests/helpers/payerCatalogue'
import { testimonialFixture } from '~/tests/helpers/storefrontFixtures'
import { StorefrontPath } from '~/tests/helpers/storefrontSection'
import { queryOf, requestsTo, routeTable, readOf } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import ProductsPage from '~/pages/products/index.vue'
import CatalogProductsHero from '~/components/catalog/ProductsHero.vue'
import CatalogProductFamilyGrid from '~/components/catalog/ProductFamilyGrid.vue'
import CatalogProductFamilyCard from '~/components/catalog/ProductFamilyCard.vue'
import CatalogProductsComparison from '~/components/catalog/ProductsComparison.vue'
import CatalogProductsBenefits from '~/components/catalog/ProductsBenefits.vue'
import CatalogProductsTestimonials from '~/components/catalog/ProductsTestimonials.vue'
import MarketingSectionHeader from '~/components/marketing/MarketingSectionHeader.vue'
import SectionsCTA from '~/components/sections/SectionsCTA.vue'
import type { PortalProduct } from '~/types/portalproduct'
import type { PortalProductGroup } from '~/types/portalproductgroup'
import { ProductType } from '~/types/producttype'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The groups: one family with products and one that is empty. */
const groups: PortalProductGroup[] = [
  { id: 3, name: 'Alpha Suite', productCount: 3 },
  { id: 4, name: 'Beta Kit', productCount: 0 },
  { id: 6, name: 'Domains', productCount: 1 },
]

/** A SaaS family: its parent and two plans, one of them priced. */
const catalogue: PortalProduct[] = [
  { id: 10, groupId: 3, name: 'Alpha Suite', slug: 'alpha-suite', type: ProductType.Vps, description: 'Learn online<br />✔ Courses' },
  { id: 20, groupId: 6, name: 'Domain Registration', slug: 'domain-registration', type: ProductType.Domain, description: 'Register' },
  { id: 11, groupId: 3, name: 'Starter', slug: 'starter', description: '✔ One site', pricing: { monthly: 10, annual: 100 } },
  { id: 12, groupId: 3, name: 'Pro', slug: 'pro', description: '✔ Two sites', pricing: { monthly: 20, annual: 200 } },
]

/** What the stood-in runtime recorded for the current test. */
let spies: PublicPageSpies

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/**
 * Mounts the page and lets the catalogue and the currencies load.
 *
 * @returns The wrapper.
 */
const mountProducts = async () => {
  const wrapper = await mountPage(ProductsPage, {
    components: {
      CatalogProductsHero,
      CatalogProductFamilyGrid,
      CatalogProductFamilyCard,
      CatalogProductsComparison,
      CatalogProductsBenefits,
      CatalogProductsTestimonials,
      MarketingSectionHeader,
      SectionsCTA,
    },
    stubs: { 'i18n-t': true },
  })
  await awaitCatalogue()
  return wrapper
}

describe('products page', () => {
  beforeEach(async () => {
    spies = stubPublicPageRuntime()
    window.localStorage.clear()
    table = {
      [`GET ${CatalogPath.BillingCurrencies}`]: { body: [USD_CURRENCY] },
      [`GET ${CatalogPath.ProductGroups}`]: { body: groups },
      [`GET ${CatalogPath.Products}`]: { body: catalogue },
      [`GET ${CatalogPath.ProductFeatures}`]: { body: [] },
      [readOf(StorefrontPath.Testimonials)]: { body: [1, 2, 3].map(id => testimonialFixture(id)) },
    }
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
    vi.unstubAllGlobals()
  })

  it('reads the products of the family groups only, and again in the payer currency once it resolves', async () => {
    await mountProducts()

    const queries = requestsTo(stub.requests, CatalogPath.Products).map(queryOf)
    expect(queries).toContainEqual(expect.objectContaining({ gids: '3,6' }))
    expect(queries.at(-1)).toEqual(expect.objectContaining({ gids: '3,6', currency: 'USD' }))
  })

  it('puts the static domains card first, then one card per catalogue family', async () => {
    const wrapper = await mountProducts()

    const cards = wrapper.findAll('[data-testid^="product-card-"]').map((card) => card.attributes('data-testid'))
    expect(cards).toEqual(['product-card-domains', 'product-card-alpha-suite'])
  })

  it('shows domains once even when the catalogue has a domains group', async () => {
    const wrapper = await mountProducts()

    expect(wrapper.findAll('[data-testid="product-card-domains"]')).toHaveLength(1)
    expect(wrapper.find('[data-testid="product-card-domain-registration"]').exists()).toBe(false)
  })

  it('gives each card the icon of its product type', async () => {
    const wrapper = await mountProducts()

    expect(wrapper.find('[data-testid="product-card-alpha-suite"]').html()).toContain('mdi:server')
    expect(wrapper.find('[data-testid="product-card-domains"]').html()).toContain('mdi:earth')
  })

  it('compares the real plans of a family with shared specification lines, in the payer currency', async () => {
    table[`GET ${CatalogPath.ProductFeatures}`] = {
      body: [
        { id: 1, productId: 11, label: 'Disk', value: '10 GB', sortOrder: 0 },
        { id: 2, productId: 12, label: 'Disk', value: '50 GB', sortOrder: 0 },
        { id: 3, productId: 11, label: 'Mail', value: '1', sortOrder: 1 },
        { id: 4, productId: 12, label: 'Mail', value: '10', sortOrder: 1 },
      ],
    }
    const wrapper = await mountProducts()

    expect(wrapper.find('[data-testid="comparison-plan-11"]').text()).toContain('Starter')
    expect(wrapper.find('[data-testid="comparison-price-11"]').text()).toContain('10.00')
    expect(wrapper.find('[data-testid="comparison-price-12"]').text()).toContain('20.00')
    expect(wrapper.find('[data-testid="comparison-row-0"]').text()).toContain('50 GB')
  })

  it('hides the comparison table when the plans share no specification lines', async () => {
    const wrapper = await mountProducts()

    expect(wrapper.find('[data-testid="products-comparison"]').exists()).toBe(false)
  })

  it('renders the hero, the benefits and the testimonials around the catalogue', async () => {
    const wrapper = await mountProducts()
    // The testimonials block reads its quotes without the page awaiting them.
    await waitUntil(() => expect(wrapper.find('[data-testid="products-testimonials"]').exists()).toBe(true))

    expect(wrapper.find('[data-testid="products-hero"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="products-benefits"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="sections-cta"]').exists()).toBe(true)
  })

  it('sets the canonical head and publishes the collection page schema', async () => {
    await mountProducts()

    expect(spies.useSeo).toHaveBeenCalledWith(expect.objectContaining({ path: '/products', title: 'seo.products.title' }))
    const schemas = spies.injectSchema.mock.calls[0]?.[0] as Array<Record<string, unknown>>
    expect(schemas.some((schema) => schema['@type'] === 'CollectionPage')).toBe(true)
  })
})
