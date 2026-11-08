/**
 * Tests for the home page: it loads the catalog for the active currency, hands the active
 * template's home component everything it renders, and wires the template's events back.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h, ref } from 'vue'
import { mountPage, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import type { PublicPageSpies } from '~/tests/helpers/publicPage'
import { PricingCycle } from '~/types/pricingcycle'

/** The fake stores, hoisted so the mocked modules can reach them. */
const stores = vi.hoisted(() => ({
  catalog: {
    loadTldPricing: vi.fn(),
    loadHostingPlans: vi.fn(),
    loadProductFeatures: vi.fn(),
    planCards: vi.fn(),
    featuresFor: vi.fn(),
  },
  currency: { payerCode: 'USD', init: vi.fn(), load: vi.fn() },
  domains: {
    searchOfferedTlds: vi.fn(),
  },
}))

vi.mock('~/stores/catalog', () => ({
  /**
   * Stands in for the catalog store.
   *
   * @returns The fake catalog store.
   */
  useCatalogStore: () => stores.catalog,
}))

vi.mock('~/stores/currency', () => ({
  /**
   * Stands in for the currency store.
   *
   * @returns The fake currency store.
   */
  useCurrencyStore: () => stores.currency,
}))

vi.mock('~/stores/domains', async () => {
  const { ref: makeRef } = await import('vue')
  const state = {
    lookupResults: makeRef([{ domain: 'a.com' }]),
    lookupPending: makeRef(false),
    offeredTlds: makeRef(['.com', '.net']),
    searchOfferedTlds: stores.domains.searchOfferedTlds,
  }
  return {
    /**
     * Stands in for the domains store.
     *
     * @returns The fake domains store.
     */
    useDomainsStore: () => state,
  }
})

const { default: Page } = await import('~/pages/index.vue')

/** Records the props the page hands to the template's home component. */
const HomeStub = defineComponent({
  name: 'HomeStub',
  props: {
    domainResults: { type: Array, default: () => [] },
    domainPending: { type: Boolean, default: false },
    hasZones: { type: Boolean, default: false },
    priceHints: { type: Array, default: () => [] },
    plans: { type: Array, default: () => [] },
    comparisonRows: { type: Array, default: () => [] },
    cycle: { type: String, default: 'monthly' },
  },
  emits: ['search', 'update:cycle'],
  setup: () => () => h('div', { 'data-testid': 'home-stub' }),
})

/** Spies of the stood-in runtime, renewed before each test. */
let spies: PublicPageSpies

describe('pages/index', () => {
  beforeEach(() => {
    spies = stubPublicPageRuntime()
    Object.values(stores.catalog).forEach(fn => fn.mockReset())
    stores.currency.init.mockReset()
    stores.currency.load.mockReset()
    stores.catalog.planCards.mockImplementation((hrefFor: (id: string) => string) => [{ id: '7', href: hrefFor('7') }])
    stores.catalog.featuresFor.mockReturnValue([{ name: 'Disk' }])
    vi.stubGlobal('useTemplate', () => ({ name: 'aurora', slot: () => HomeStub }))
    vi.stubGlobal('buildComparisonRows', vi.fn((features: unknown[], ids: string[]) => [{ features, ids }]))
    vi.stubGlobal('useAsyncData', vi.fn(async (_key: string, handler: () => Promise<unknown>) => {
      await handler()
      return { pending: ref(false) }
    }))
  })

  it('sets the home tags and publishes organisation, business and site structured data', async () => {
    await mountPage(Page)

    expect(spies.useSeo).toHaveBeenCalledWith(expect.objectContaining({ title: 'seo.home.title', path: '/' }))
    const published = spies.injectSchema.mock.calls[0]?.[0] as Array<Record<string, unknown>>
    expect(published.map(entry => entry['@type'])).toEqual(['Organization', 'LocalBusiness', 'WebSite'])
  })

  it('reads the TLD prices, the hosting plans and the hosting features', async () => {
    await mountPage(Page)

    expect(stores.catalog.loadTldPricing).toHaveBeenCalledTimes(1)
    expect(stores.catalog.loadHostingPlans).toHaveBeenCalledTimes(1)
    expect(stores.catalog.loadProductFeatures).toHaveBeenCalledTimes(1)
  })

  it('restores the guest currency once mounted', async () => {
    await mountPage(Page)

    expect(stores.currency.init).toHaveBeenCalledTimes(1)
  })

  it('loads the billing currencies during the server render, before the plans', async () => {
    await mountPage(Page)

    expect(stores.currency.load).toHaveBeenCalledTimes(1)
    expect(stores.currency.load.mock.invocationCallOrder[0]).toBeLessThan(stores.catalog.loadHostingPlans.mock.invocationCallOrder[0]!)
  })

  it('renders the template\'s home component with the domain lookup state', async () => {
    const wrapper = await mountPage(Page)

    const home = wrapper.findComponent(HomeStub)
    expect(home.exists()).toBe(true)
    expect(home.props('hasZones')).toBe(true)
    expect(home.props('priceHints')).toEqual(['.com', '.net'])
    expect(home.props('domainResults')).toEqual([{ domain: 'a.com' }])
    expect(home.props('domainPending')).toBe(false)
  })

  it('builds the plan cards with localised configure links', async () => {
    const wrapper = await mountPage(Page)

    expect(wrapper.findComponent(HomeStub).props('plans')).toEqual([{ id: '7', href: '/en/configure/7' }])
  })

  it('assembles the comparison rows from the features and the plan ids', async () => {
    const wrapper = await mountPage(Page)

    expect(wrapper.findComponent(HomeStub).props('comparisonRows')).toEqual([{ features: [{ name: 'Disk' }], ids: ['7'] }])
  })

  it('runs a domain search when the template asks for one', async () => {
    const wrapper = await mountPage(Page)

    wrapper.findComponent(HomeStub).vm.$emit('search', 'example')

    expect(stores.domains.searchOfferedTlds).toHaveBeenCalledWith('example')
  })

  it('switches the billing cycle when the template changes it', async () => {
    const wrapper = await mountPage(Page)
    expect(wrapper.findComponent(HomeStub).props('cycle')).toBe(PricingCycle.Monthly)

    wrapper.findComponent(HomeStub).vm.$emit('update:cycle', PricingCycle.Quarterly)
    await wrapper.vm.$nextTick()

    expect(wrapper.findComponent(HomeStub).props('cycle')).toBe(PricingCycle.Quarterly)
  })
})
