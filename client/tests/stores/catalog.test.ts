/**
 * Tests for `stores/catalog.ts`.
 *
 * The store, not the public pages, owns the product lists, the loading flag and the failure
 * sentence, and it always asks for prices in the payer's own currency, so these assertions pin
 * the contract the pages render from.
 *
 * @module tests/stores/catalog.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { balancedColumns, lastRowSize } from '~/utils/balancedGrid'

/** The mocked catalogue endpoints, re-created for every test. */
const api = {
  fetchProducts: vi.fn(),
  fetchProductGroups: vi.fn(),
  fetchProductFeatures: vi.fn(),
  fetchSlides: vi.fn(),
  fetchCountries: vi.fn(),
  fetchTldPricing: vi.fn(),
  fetchCurrencies: vi.fn(),
  checkDomain: vi.fn()
}

/** The payer the currency store stand-in reports; tests change it to simulate a currency switch. */
const payer = { code: 'USD' as string | null }

vi.mock('~/composables/apis/useCatalogApi', () => ({
  /**
   * Stands in for the real API composable so the store runs without transport.
   *
   * @returns The mocked endpoints.
   */
  useCatalogApi: () => api
}))

vi.mock('~/stores/currency', () => ({
  /**
   * Stands in for the currency store: a payer code and the money shape for formatting.
   *
   * @returns The two members the catalogue store reads.
   */
  useCurrencyStore: () => ({
    get payerCode() {
      return payer.code
    },
    moneyCurrencyFor: () => ({ code: 'USD', prefix: '$', suffix: '', decimals: 2 })
  })
}))

const { useCatalogStore } = await import('~/stores/catalog')
const { formatMoney } = await import('~/utils/formatMoney')

/** An API refusal as `apiErrorMessage` reads it. */
const refusal = { data: { detail: 'Catalogue unavailable.' } }

/** The money shape the currency stand-in returns. */
const usd = { code: 'USD', prefix: '$', suffix: '', decimals: 2 }

describe('useCatalogStore.loadProducts', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    payer.code = 'USD'
    Object.values(api).forEach(fn => fn.mockReset())
  })

  it('asks for the filter in the payer currency and keeps the list', async () => {
    api.fetchProducts.mockResolvedValue([{ id: 1, name: 'Starter' }])
    const store = useCatalogStore()

    const list = await store.loadProducts({ gid: 1 })

    expect(api.fetchProducts).toHaveBeenCalledWith({ gid: 1, currency: 'USD' })
    expect(list).toEqual([{ id: 1, name: 'Starter' }])
    expect(store.productsFor({ gid: 1 })).toEqual(list)
    expect(store.loading).toBe(false)
    expect(store.error).toBe('')
  })

  it('omits the currency while the payer currency is not known yet', async () => {
    payer.code = null
    api.fetchProducts.mockResolvedValue([])
    const store = useCatalogStore()

    await store.loadProducts({ gids: '1,3' })

    expect(api.fetchProducts).toHaveBeenCalledWith({ gids: '1,3', currency: undefined })
  })

  it('keeps one list per filter and per currency', async () => {
    api.fetchProducts.mockResolvedValueOnce([{ id: 1 }]).mockResolvedValueOnce([{ id: 2 }])
    const store = useCatalogStore()

    await store.loadProducts({ gid: 1 })
    payer.code = 'AMD'
    await store.loadProducts({ gid: 1 })

    expect(store.productsFor({ gid: 1 })).toEqual([{ id: 2 }])
    payer.code = 'USD'
    expect(store.productsFor({ gid: 1 })).toEqual([{ id: 1 }])
  })

  it('keeps the API sentence and an empty list when the read is refused', async () => {
    api.fetchProducts.mockRejectedValue(refusal)
    const store = useCatalogStore()

    const list = await store.loadProducts({ gid: 1 })

    expect(list).toEqual([])
    expect(store.error).toBe('Catalogue unavailable.')
    expect(store.loading).toBe(false)
  })

  it('clears the previous sentence when the next read succeeds', async () => {
    api.fetchProducts.mockRejectedValueOnce(refusal).mockResolvedValueOnce([])
    const store = useCatalogStore()

    await store.loadProducts()
    await store.loadProducts()

    expect(store.error).toBe('')
  })

  it('finds the domain product by name in the whole catalogue', async () => {
    api.fetchProducts.mockResolvedValue([{ id: 4, name: 'Hosting' }, { id: 9, name: 'Domain Registration' }])
    const store = useCatalogStore()
    expect(store.domainProductId).toBeNull()

    await store.loadProducts()

    expect(store.domainProductId).toBe(9)
  })
})

describe('useCatalogStore product families', () => {
  /** The groups the API answers with: hosting, one family and an empty group. */
  const groups = [
    { id: 3, name: 'Alpha Suite', productCount: 2 },
    { id: 4, name: 'Beta Kit', productCount: 0 },
    { id: 1, name: 'Web Hosting', productCount: 3 },
  ]

  beforeEach(() => {
    setActivePinia(createPinia())
    payer.code = 'USD'
    Object.values(api).forEach(fn => fn.mockReset())
  })

  it('keeps the groups and leaves the sentence empty when the read succeeds', async () => {
    api.fetchProductGroups.mockResolvedValue(groups)
    const store = useCatalogStore()

    await store.loadProductGroups()

    expect(store.productGroups).toEqual(groups)
    expect(store.error).toBe('')
  })

  it('shows only active, non-empty groups as families, with hosting first', async () => {
    api.fetchProductGroups.mockResolvedValue(groups)
    const store = useCatalogStore()

    await store.loadProductGroups()

    expect(store.familyGroups.map(group => group.id)).toEqual([1, 3])
  })

  it('reads the products of the family groups in the payer currency and keeps them', async () => {
    api.fetchProductGroups.mockResolvedValue(groups)
    api.fetchProducts.mockResolvedValue([{ id: 10, groupId: 3, name: 'Alpha Suite' }])
    const store = useCatalogStore()

    const products = await store.loadFamilies()

    expect(api.fetchProducts).toHaveBeenCalledWith({ gids: '1,3', currency: 'USD' })
    expect(products).toHaveLength(1)
    expect(store.familyProducts).toEqual(products)
  })

  it('asks for no products while there is no family, so the endpoint is not asked for everything', async () => {
    api.fetchProductGroups.mockResolvedValue([groups[1]])
    const store = useCatalogStore()

    expect(await store.loadFamilies()).toEqual([])
    expect(api.fetchProducts).not.toHaveBeenCalled()
    expect(store.familyProducts).toEqual([])
  })

  it('shares one read between callers that ask while it is in flight, and reads again afterwards', async () => {
    api.fetchProductGroups.mockResolvedValue([groups[1]])
    const store = useCatalogStore()

    const [first, second] = await Promise.all([store.loadFamilies(), store.loadFamilies()])

    expect(first).toBe(second)
    expect(api.fetchProductGroups).toHaveBeenCalledTimes(1)

    await store.loadFamilies()
    expect(api.fetchProductGroups).toHaveBeenCalledTimes(2)
  })

  it('keeps the API sentence and no families when the groups cannot be read', async () => {
    api.fetchProductGroups.mockRejectedValue(refusal)
    const store = useCatalogStore()

    expect(await store.loadFamilies()).toEqual([])
    expect(store.productGroups).toEqual([])
    expect(store.error).toBe('Catalogue unavailable.')
  })
})

describe('useCatalogStore.planCards', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    payer.code = 'USD'
    Object.values(api).forEach(fn => fn.mockReset())
  })

  it('maps hosting plans to priced cards with the page-supplied link', async () => {
    api.fetchProducts.mockResolvedValue([
      { id: 5, name: 'Pro', description: 'Fast hosting', pricing: { monthly: 12, annual: 120 } },
      { id: 6, name: 'Free', description: '', pricing: { monthly: 0, annual: 0 } },
      { id: 7, name: 'Quote', description: '', pricing: null }
    ])
    const store = useCatalogStore()
    await store.loadHostingPlans()

    const cards = store.planCards(id => `/en/configure/${id}`)

    expect(cards[1]).toMatchObject({
      id: 5,
      name: 'Pro',
      description: 'Fast hosting',
      isFree: false,
      priceMonthly: formatMoney(12, usd),
      priceAnnual: formatMoney(10, usd),
      href: '/en/configure/5'
    })
    expect(cards[0]?.isFree).toBe(true)
    expect(cards[2]).toMatchObject({ isFree: false, priceMonthly: '—', priceAnnual: '—' })
  })

  it('prices a card per month on every cycle the product is sold on and leaves the others out', async () => {
    api.fetchProducts.mockResolvedValue([
      { id: 5, name: 'Pro', description: '', pricing: { monthly: 12, quarterly: 30, semiAnnual: 54, annual: null, biennial: 192 } }
    ])
    const store = useCatalogStore()
    await store.loadHostingPlans()

    const [card] = store.planCards(id => `/${id}`)

    expect(card?.cyclePrices).toEqual({
      monthly: formatMoney(12, usd),
      quarterly: formatMoney(10, usd),
      semiAnnual: formatMoney(9, usd),
      biennial: formatMoney(8, usd)
    })
    expect(card?.cyclePrices.annual).toBeUndefined()
  })

  it('gives a product with no pricing no cycle prices', async () => {
    api.fetchProducts.mockResolvedValue([{ id: 7, name: 'Quote', description: '', pricing: null }])
    const store = useCatalogStore()
    await store.loadHostingPlans()

    expect(store.planCards(id => `/${id}`)[0]?.cyclePrices).toEqual({})
  })

  it('lists a plan\'s own specification lines when its description carries no feature lines', async () => {
    api.fetchProducts.mockResolvedValue([
      { id: 5, name: 'Pro', description: 'Fast hosting', pricing: { monthly: 12, annual: 120 } },
      { id: 6, name: 'Plus', description: 'Faster', pricing: { monthly: 20, annual: 200 } }
    ])
    const store = useCatalogStore()
    await store.loadHostingPlans()

    const cards = store.planCards(id => `/${id}`, [
      { id: 1, productId: 5, label: 'Storage', value: '50 GB', sortOrder: 0 },
      { id: 2, productId: 6, label: 'Storage', value: '90 GB', sortOrder: 0 }
    ])

    expect(cards.map(card => card.features)).toEqual([['Storage: 50 GB'], ['Storage: 90 GB']])
  })

  it('orders plans by price, cheapest first, when the API gives no sort order', async () => {
    api.fetchProducts.mockResolvedValue([
      { id: 1, name: 'Business', pricing: { monthly: 9.99, annual: 99 } },
      { id: 2, name: 'Starter', pricing: { monthly: 4.99, annual: 49 } },
      { id: 3, name: 'Pro', pricing: { monthly: 19.99, annual: 199 } },
      { id: 4, name: 'Quote', pricing: null }
    ])
    const store = useCatalogStore()
    await store.loadHostingPlans()

    expect(store.planCards(id => `/${id}`).map(card => card.name)).toEqual(['Starter', 'Business', 'Pro', 'Quote'])
  })

  it('lets an explicit sort order win over price', async () => {
    api.fetchProducts.mockResolvedValue([
      { id: 1, name: 'Cheap', sortOrder: 2, pricing: { monthly: 1, annual: 10 } },
      { id: 2, name: 'Dear', sortOrder: 1, pricing: { monthly: 50, annual: 500 } },
      { id: 3, name: 'Unordered', pricing: { monthly: 0.5, annual: 5 } }
    ])
    const store = useCatalogStore()
    await store.loadHostingPlans()

    expect(store.planCards(id => `/${id}`).map(card => card.name)).toEqual(['Dear', 'Cheap', 'Unordered'])
  })

  it('marks only the first featured paid plan, in display order, as recommended', async () => {
    api.fetchProducts.mockResolvedValue([
      { id: 1, name: 'Free', isFeatured: true, sortOrder: 0, pricing: { monthly: 0, annual: 0 } },
      { id: 2, name: 'Late', isFeatured: true, sortOrder: 5, pricing: { monthly: 9, annual: 90 } },
      { id: 3, name: 'Early', isFeatured: true, sortOrder: 2, pricing: { monthly: 19, annual: 190 } },
      { id: 4, name: 'Plain', sortOrder: 3, pricing: { monthly: 29, annual: 290 } }
    ])
    const store = useCatalogStore()
    await store.loadHostingPlans()

    const cards = store.planCards(id => `/${id}`)

    expect(cards.filter(card => card.isFeatured).map(card => card.name)).toEqual(['Early'])
  })

  it('marks no plan when none is featured', async () => {
    api.fetchProducts.mockResolvedValue([
      { id: 1, name: 'A', pricing: { monthly: 5, annual: 50 } },
      { id: 2, name: 'B', isFeatured: false, pricing: { monthly: 9, annual: 90 } }
    ])
    const store = useCatalogStore()
    await store.loadHostingPlans()

    expect(store.planCards(id => `/${id}`).some(card => card.isFeatured)).toBe(false)
  })

  it('shows prod-shaped plans (no featured flag, default sort order) without a badge, ordered by price, in a balanced grid', async () => {
    // Real operator plans: every sortOrder is the default 0, nothing is featured, the API order is arbitrary.
    api.fetchProducts.mockResolvedValue([
      { id: 14, name: 'Business', sortOrder: 0, pricing: { monthly: 15, annual: 150 } },
      { id: 11, name: 'Free', sortOrder: 0, pricing: { monthly: 0, annual: 0 } },
      { id: 16, name: 'Agency', sortOrder: 0, pricing: { monthly: 40, annual: 400 } },
      { id: 12, name: 'Starter', sortOrder: 0, pricing: { monthly: 5, annual: 50 } },
      { id: 15, name: 'Pro', sortOrder: 0, pricing: { monthly: 25, annual: 250 } },
      { id: 13, name: 'Webmaster', sortOrder: 0, pricing: { monthly: 9, annual: 90 } }
    ])
    const store = useCatalogStore()
    await store.loadHostingPlans()

    const cards = store.planCards(id => `/${id}`)

    expect(cards.map(card => card.name)).toEqual(['Free', 'Starter', 'Webmaster', 'Business', 'Pro', 'Agency'])
    expect(cards.some(card => card.isFeatured)).toBe(false)
    expect(balancedColumns(cards.length)).toBe(3)
    expect(lastRowSize(cards.length, balancedColumns(cards.length))).toBe(3)
  })

  it('has no cards before the plans were read', () => {
    expect(useCatalogStore().planCards(id => `/${id}`)).toEqual([])
  })
})

describe('useCatalogStore reference data', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    payer.code = 'USD'
    Object.values(api).forEach(fn => fn.mockReset())
  })

  it('keeps specification lines per product group', async () => {
    api.fetchProductFeatures.mockResolvedValue([{ id: 1, productId: 5, label: 'Disk', value: '10 GB', sortOrder: 0 }])
    const store = useCatalogStore()

    await store.loadProductFeatures(1)

    expect(api.fetchProductFeatures).toHaveBeenCalledWith(1)
    expect(store.featuresFor(1)).toHaveLength(1)
    expect(store.featuresFor(2)).toEqual([])
  })

  it('reads the slides in the given language', async () => {
    api.fetchSlides.mockResolvedValue([{ id: 1, title: 'Hello' }])
    const store = useCatalogStore()

    await store.loadSlides('hy')

    expect(api.fetchSlides).toHaveBeenCalledWith('hy')
    expect(store.slides).toEqual([{ id: 1, title: 'Hello' }])
  })

  it('reads the countries and empties the list when the read is refused', async () => {
    api.fetchCountries.mockResolvedValueOnce([{ value: 'AM', label: 'Armenia' }]).mockRejectedValueOnce(refusal)
    const store = useCatalogStore()

    await store.loadCountries()
    expect(store.countries).toEqual([{ value: 'AM', label: 'Armenia' }])

    await store.loadCountries()
    expect(store.countries).toEqual([])
    expect(store.error).toBe('Catalogue unavailable.')
  })

  it('reads the TLD price table in the payer currency and nulls it on a refusal', async () => {
    api.fetchTldPricing.mockResolvedValueOnce({ currency: { code: 'USD', prefix: '$' }, pricing: {} }).mockRejectedValueOnce(refusal)
    const store = useCatalogStore()

    await store.loadTldPricing()
    expect(api.fetchTldPricing).toHaveBeenCalledWith('USD')
    expect(store.tldPricing).toMatchObject({ currency: { code: 'USD' } })

    await store.loadTldPricing()
    expect(store.tldPricing).toBeNull()
  })
})

describe('useCatalogStore keeps what it read when a refresh fails', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    payer.code = 'USD'
    Object.values(api).forEach(fn => fn.mockReset())
  })

  it('keeps the groups and the family products after a refused refresh', async () => {
    api.fetchProductGroups.mockResolvedValueOnce([{ id: 1, name: 'Web Hosting', productCount: 1 }])
    api.fetchProducts.mockResolvedValueOnce([{ id: 10, groupId: 1, name: 'Plan' }])
    const store = useCatalogStore()
    await store.loadFamilies()

    api.fetchProductGroups.mockRejectedValue(new Error('down'))
    api.fetchProducts.mockRejectedValue(new Error('down'))
    const again = await store.loadFamilies()

    expect(store.familyGroups).toHaveLength(1)
    expect(again).toHaveLength(1)
    expect(store.familyProducts).toHaveLength(1)
    expect(store.error).not.toBe('')
  })

  it('does not hand a caller the read that was started for another currency', async () => {
    api.fetchProductGroups.mockResolvedValue([{ id: 1, name: 'Web Hosting', productCount: 1 }])
    api.fetchProducts.mockResolvedValue([])
    const store = useCatalogStore()

    const usd = store.loadFamilies()
    payer.code = 'AMD'
    const amd = store.loadFamilies()
    await Promise.all([usd, amd])

    // Two reads ran: the second caller was not given the first one's promise.
    expect(api.fetchProductGroups).toHaveBeenCalledTimes(2)
    expect(api.fetchProducts).toHaveBeenCalledTimes(2)
    expect(store.familyProducts).toEqual([])
  })
})
