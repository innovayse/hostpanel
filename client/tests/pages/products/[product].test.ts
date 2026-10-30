/**
 * Unit tests for the product detail page: it finds the family named by the address and names the page
 * after the family, shows its plans (description, features, price) in the payer's currency, adds a plan
 * to the cart in the chosen cycle, links the external order page only when the deployment has one,
 * compares the plans when their specifications allow it, publishes the family as structured data,
 * and answers a missing product with a notice.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { mountPage, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import ProductPage from '~/pages/products/[product].vue'
import CatalogProductDetail from '~/components/catalog/ProductDetail.vue'
import CatalogProductPlanCard from '~/components/catalog/ProductPlanCard.vue'
import CatalogProductPlansTable from '~/components/catalog/ProductPlansTable.vue'
import CatalogProductSectionHeader from '~/components/catalog/ProductSectionHeader.vue'
import { useCartStore } from '~/stores/cart'
import type { BillingCurrency } from '~/types/currency'
import type { PortalProduct } from '~/types/portalproduct'
import type { PortalProductGroup } from '~/types/portalproductgroup'
import type { ProductFeature } from '~/types/productfeature'
import { ProductType } from '~/types/producttype'

/** The mocked catalogue endpoints, re-created for every test. */
const api = { fetchProducts: vi.fn(), fetchProductGroups: vi.fn(), fetchProductFeatures: vi.fn() }

/** The currency the stood-in currency endpoint answers with. */
const usd: BillingCurrency = { code: 'USD', numeric: '840', prefix: '$', suffix: '', decimals: 2, isBase: true }

vi.mock('~/composables/apis/useCatalogApi', () => ({
  /**
   * Stands in for the catalogue API composable so the store runs without transport.
   *
   * @returns The mocked endpoints.
   */
  useCatalogApi: () => api,
}))

vi.mock('~/composables/apis/useCurrencyApi', () => ({
  /**
   * Stands in for the currency API composable.
   *
   * @returns Only the endpoint the page's store reaches for.
   */
  useCurrencyApi: () => ({ loadBillingCurrencies: () => Promise.resolve([usd]) }),
}))

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

/** The spies of the latest mount, so a test reads the SEO tags the page published. */
let spies: ReturnType<typeof stubPublicPageRuntime>

/**
 * Mounts the page for one product address.
 *
 * @param product The `product` route parameter.
 * @returns The wrapper.
 */
const mountProduct = async (product = 'alpha-suite') => {
  spies = stubPublicPageRuntime({ params: { product } })
  const wrapper = await mountPage(ProductPage, { components: { CatalogProductDetail, CatalogProductPlanCard, CatalogProductPlansTable, CatalogProductSectionHeader }, stubs: { 'i18n-t': true, SectionsFAQ: true } })
  await flushPromises()
  return wrapper
}

describe('product detail page', () => {
  beforeEach(() => {
    api.fetchProducts.mockReset()
    api.fetchProducts.mockResolvedValue(catalogue)
    api.fetchProductGroups.mockReset()
    api.fetchProductGroups.mockResolvedValue(groups)
    api.fetchProductFeatures.mockReset()
    api.fetchProductFeatures.mockResolvedValue([])
    window.localStorage.clear()
    vi.stubGlobal('open', vi.fn())
    vi.stubGlobal('useRuntimeConfig', () => ({ public: { baseUrl: 'https://panel.test' } }))
  })

  it('builds its absolute SEO URLs from the configured site URL', async () => {
    await mountProduct()

    const tags = spies.useSeoMeta.mock.calls.at(-1)?.[0] as { ogImage: string, ogUrl: string }
    expect(tags.ogImage).toBe('https://panel.test/og-image.jpg')
    expect(tags.ogUrl).toMatch(/^https:\/\/panel\.test\//)
  })

  it('shows the product named by the address with its plans and their monthly prices', async () => {
    const wrapper = await mountProduct()

    expect(wrapper.find('[data-testid="product-name"]').text()).toBe('Alpha Suite')
    expect(wrapper.find('[data-testid="product-plan-11"] [data-testid="product-plan-price"]').text()).toContain('10.00')
    expect(wrapper.find('[data-testid="product-plan-12"] [data-testid="product-plan-price"]').text()).toContain('20.00')
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
    api.fetchProductFeatures.mockResolvedValue(specifications)
    const wrapper = await mountProduct()

    expect(api.fetchProductFeatures).toHaveBeenCalledWith(3)
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

    const documents = spies.injectSchema.mock.calls.at(-1)?.[0] as Array<{ '@type': string, family?: { name: string, url: string, offers: unknown[] } }>
    const family = documents.find((document) => document['@type'] === 'Product')?.family

    expect(family).toMatchObject({ name: 'Alpha Suite', url: 'https://panel.test/products/alpha-suite' })
    expect(family?.offers).toEqual([
      { name: 'Starter', price: 10, priceCurrency: 'USD', billingDuration: 'P1M', url: 'https://panel.test/products/alpha-suite' },
      { name: 'Pro', price: 20, priceCurrency: 'USD', billingDuration: 'P1M', url: 'https://panel.test/products/alpha-suite' },
    ])
  })

  it('adds a plan to the cart with the selected cycle and the payer currency', async () => {
    const wrapper = await mountProduct()

    await wrapper.find('[data-testid="product-add-11"]').trigger('click')

    expect(useCartStore().items[0]).toMatchObject({ pid: 11, name: 'Starter', billingcycle: 'monthly', amount: 10, currency: 'USD' })
  })

  it('keeps the type of a digital plan on its cart line, and none for a plan without one', async () => {
    api.fetchProducts.mockResolvedValue(catalogue.map((entry) => (entry.id === 11 ? { ...entry, type: ProductType.Digital } : entry)))
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
    vi.stubGlobal('useRuntimeConfig', () => ({ public: { billingPortalUrl: 'https://billing.test' } }))
    const wrapper = await mountProduct()

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
