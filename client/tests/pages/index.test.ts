/**
 * Tests for the home page against the real catalogue, currency and domains stores, the real API
 * composables and the real transport, with a stub BFF answering on a real socket: it loads the catalog
 * for the active currency, hands the active template's home component everything it renders, and wires
 * the template's events back.
 *
 * @module tests/pages/index.test
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import type { PublicPageSpies } from '~/tests/helpers/publicPage'
import { CatalogPath } from '~/tests/helpers/catalogPaths'
import { USD_CURRENCY } from '~/tests/helpers/payerCatalogue'
import { bodyOf } from '~/tests/helpers/requestBody'
import { mountRenderedPage } from '~/tests/helpers/renderedPage'
import { until } from '~/tests/helpers/settle'
import { pathOf, requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { useCurrencyStore } from '~/stores/currency'
import { useDomainsStore } from '~/stores/domains'
import { DomainAvailabilityStatus } from '~/types/domainavailabilitystatus'
import { PricingCycle } from '~/types/pricingcycle'
import type { ProductFeature } from '~/types/productfeature'
import { buildComparisonRows } from '~/utils/comparisonRows'

const { default: Page } = await import('~/pages/index.vue')

/** The currency store's storage key, which the guest's choice is kept under. */
const CURRENCY_STORAGE_KEY = 'innovayse_currency'

/** The one hosting plan the catalogue answers with. */
const PLAN = { id: 7, groupId: 1, name: 'Starter', description: 'Small sites', pricing: { monthly: 5, annual: 50 } }

/** The specification line of that plan, which the comparison rows are built from. */
const FEATURE: ProductFeature = { id: 1, productId: 7, label: 'Disk', value: '10 GB', sortOrder: 1 }

/** The extensions the operator offers, in the order the search box hints them. */
const OFFERED_TLDS = ['com', 'net']

/** The price table the catalogue answers with: one year of registration for each offered extension. */
const TLD_PRICING = {
  currency: { code: USD_CURRENCY.code, prefix: USD_CURRENCY.prefix },
  pricing: OFFERED_TLDS.map(tld => ({ tld, sellCurrency: USD_CURRENCY.code, register: [{ years: 1, amount: 10 }] })),
}

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

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/**
 * Mounts the page and waits until the template's home component is on screen, which is after the
 * currencies, the TLD prices, the hosting plans and their features have been read.
 *
 * @returns The wrapper.
 */
const mountHome = () => mountRenderedPage(Page, 'home-stub')

describe('pages/index', () => {
  beforeEach(async () => {
    spies = stubPublicPageRuntime()
    window.localStorage.clear()
    vi.stubGlobal('useTemplate', () => ({ name: 'aurora', slot: () => HomeStub }))
    vi.stubGlobal('buildComparisonRows', buildComparisonRows)
    table = {
      [`GET ${CatalogPath.BillingCurrencies}`]: { body: [USD_CURRENCY] },
      [`GET ${CatalogPath.TldPricing}`]: { body: TLD_PRICING },
      [`GET ${CatalogPath.Products}`]: { body: [PLAN] },
      [`GET ${CatalogPath.ProductFeatures}`]: { body: [FEATURE] },
      [`POST ${CatalogPath.DomainCheck}`]: request => ({
        body: { domain: (bodyOf(request) as { domain: string }).domain, available: true, status: DomainAvailabilityStatus.Available },
      }),
    }
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
    vi.unstubAllGlobals()
  })

  it('sets the home tags and publishes organisation, business and site structured data', async () => {
    await mountHome()

    expect(spies.useSeo).toHaveBeenCalledWith(expect.objectContaining({ title: 'seo.home.title', path: '/' }))
    const published = spies.injectSchema.mock.calls[0]?.[0] as Array<Record<string, unknown>>
    expect(published.map(entry => entry['@type'])).toEqual(['Organization', 'LocalBusiness', 'WebSite'])
  })

  it('reads the TLD prices, the hosting plans and the hosting features', async () => {
    await mountHome()

    expect(requestsTo(stub.requests, CatalogPath.TldPricing)).toHaveLength(1)
    expect(requestsTo(stub.requests, CatalogPath.Products)).toHaveLength(1)
    expect(requestsTo(stub.requests, CatalogPath.ProductFeatures)).toHaveLength(1)
  })

  it('restores the guest currency once mounted', async () => {
    window.localStorage.setItem(CURRENCY_STORAGE_KEY, USD_CURRENCY.code)

    await mountHome()

    expect(useCurrencyStore().selectedCode).toBe(USD_CURRENCY.code)
  })

  it('loads the billing currencies during the server render, before the plans', async () => {
    await mountHome()

    expect(requestsTo(stub.requests, CatalogPath.BillingCurrencies)).toHaveLength(1)
    const order = stub.requests.map(pathOf)
    expect(order.indexOf(CatalogPath.BillingCurrencies)).toBeGreaterThanOrEqual(0)
    expect(order.indexOf(CatalogPath.BillingCurrencies)).toBeLessThan(order.indexOf(CatalogPath.Products))
  })

  it('renders the template\'s home component with the domain lookup state', async () => {
    const wrapper = await mountHome()

    const home = wrapper.findComponent(HomeStub)
    expect(home.exists()).toBe(true)
    expect(home.props('hasZones')).toBe(true)
    expect((home.props('priceHints') as Array<{ tld: string }>).map(hint => hint.tld)).toEqual(['.com', '.net'])
    expect(home.props('domainResults')).toEqual([])
    expect(home.props('domainPending')).toBe(false)
  })

  it('builds the plan cards with localised configure links', async () => {
    const wrapper = await mountHome()

    const cards = wrapper.findComponent(HomeStub).props('plans') as Array<{ id: number, href: string }>
    expect(cards.map(card => ({ id: card.id, href: card.href }))).toEqual([{ id: 7, href: '/en/configure/7' }])
  })

  it('assembles the comparison rows from the features and the plan ids', async () => {
    const wrapper = await mountHome()

    expect(wrapper.findComponent(HomeStub).props('comparisonRows')).toEqual([{ label: 'Disk', values: ['10 GB'] }])
  })

  it('runs a domain search when the template asks for one', async () => {
    const wrapper = await mountHome()

    wrapper.findComponent(HomeStub).vm.$emit('search', 'example')
    await until(() => requestsTo(stub.requests, CatalogPath.DomainCheck).length === OFFERED_TLDS.length && !useDomainsStore().lookupPending, 'the lookup to finish')
    await flushPromises()

    const checked = requestsTo(stub.requests, CatalogPath.DomainCheck).map(request => bodyOf(request))
    expect(checked).toEqual(OFFERED_TLDS.map(tld => ({ domain: `example.${tld}` })))
    const results = wrapper.findComponent(HomeStub).props('domainResults') as Array<{ name: string }>
    expect(results.map(result => result.name)).toEqual(OFFERED_TLDS.map(tld => `example.${tld}`))
  })

  it('switches the billing cycle when the template changes it', async () => {
    const wrapper = await mountHome()
    expect(wrapper.findComponent(HomeStub).props('cycle')).toBe(PricingCycle.Monthly)

    wrapper.findComponent(HomeStub).vm.$emit('update:cycle', PricingCycle.Quarterly)
    await wrapper.vm.$nextTick()

    expect(wrapper.findComponent(HomeStub).props('cycle')).toBe(PricingCycle.Quarterly)
  })
})
