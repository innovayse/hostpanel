/**
 * Unit tests for the public domains page against the real catalogue, currency, cart and domains
 * stores, API composables and transport, with a stub BFF answering on a real socket (nothing of ours
 * is mocked): it hands the template's component the price table, the lookup state and the cart, adds
 * a registration to the cart only when it can be sold in the payer's currency, and sets the head and
 * the structured data.
 *
 * @module tests/pages/domains/index.test
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { defineComponent, h, ref } from 'vue'
import { mountPage, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import type { PublicPageSpies } from '~/tests/helpers/publicPage'
import { CatalogPath } from '~/tests/helpers/catalogPaths'
import { USD_CURRENCY, awaitCatalogue } from '~/tests/helpers/payerCatalogue'
import { bodyOf } from '~/tests/helpers/requestBody'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { until } from '~/tests/helpers/settle'
import DomainsPage from '~/pages/domains/index.vue'
import { useCartStore } from '~/stores/cart'
import { useDomainsStore } from '~/stores/domains'
import { ALL_CATEGORY } from '~/templates/aurora/types/allcategory'
import { DomainResultStatus } from '~/templates/aurora/types/domainresultstatus'
import type { TldPricing } from '~/types/tldpricing'
import { CartItemType } from '~/types/cartitemtype'
import { DomainOrderAction } from '~/types/domainorderaction'
import { DOMAIN_SEARCH_QUERY_PARAM } from '~/constants/domainSearchQuery'
import { TemplateName } from '~/types/templatename'

/** The public reads of the domains page that no catalogue helper names. */
const DomainPath = {
  TldPricing: '/api/portal/public/tld-pricing',
  DomainCheck: '/api/portal/public/domain-check',
} as const

/**
 * Builds a TLD price table with one extension.
 *
 * @param sellCurrency The currency the extension sells in.
 * @returns The table.
 */
const pricing = (sellCurrency: string): TldPricing => ({
  currency: { code: 'USD', prefix: '$' },
  pricing: [{ tld: '.com', sellCurrency, register: [{ years: 1, amount: 10 }], categories: ['popular'] }],
})

/** What the template component is given, recorded for the assertions. */
const received = { props: {} as Record<string, unknown> }

/** Stand-in for the template's domains component: records its props. */
const TemplateDomains = defineComponent({
  name: 'TemplateDomains',
  props: {
    priceRows: { type: Array, default: () => [] },
    results: { type: Array, default: () => [] },
    pending: { type: Boolean, default: false },
    categories: { type: Array, default: () => [] },
    activeCategory: { type: String, default: '' },
    inCart: { type: Array, default: () => [] },
  },
  emits: ['search', 'update:category', 'add'],
  setup(props) {
    received.props = props
    return () => h('div', { 'data-testid': 'template-domains' })
  },
})

/** What the stood-in runtime recorded for the current test. */
let spies: PublicPageSpies

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/**
 * The domain checks the stub has received.
 *
 * @returns The requests, oldest first.
 */
const domainChecks = () => requestsTo(stub.requests, DomainPath.DomainCheck)

/**
 * Mounts the page and waits until the price table, the catalogue and the currencies have been read.
 *
 * @param template Template the stood-in `useTemplate` reports.
 * @returns The wrapper.
 */
const mountDomains = async (template: TemplateName = TemplateName.Aurora) => {
  vi.stubGlobal('useTemplate', () => ({ slot: () => TemplateDomains, name: ref(template) }))
  const wrapper = await mountPage(DomainsPage)
  await awaitCatalogue()
  return wrapper
}

describe('domains page', () => {
  beforeEach(async () => {
    spies = stubPublicPageRuntime()
    vi.stubGlobal('useTemplate', () => ({ slot: () => TemplateDomains }))
    window.localStorage.clear()
    table = {
      [`GET ${CatalogPath.Products}`]: { body: [{ id: 7, groupId: 2, name: 'Domain Registration' }] },
      [`GET ${DomainPath.TldPricing}`]: { body: pricing('USD') },
      [`GET ${CatalogPath.BillingCurrencies}`]: { body: [USD_CURRENCY] },
      [`POST ${DomainPath.DomainCheck}`]: { body: { domain: 'example.com', available: true } },
    }
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('hands the template the price rows, the categories and the all-categories filter', async () => {
    await mountDomains()

    expect((received.props.priceRows as Array<{ tld: string }>).map((row) => row.tld)).toEqual(['.com'])
    expect(received.props.categories).toEqual(expect.arrayContaining([ALL_CATEGORY, 'popular']))
    expect(received.props.activeCategory).toBe(ALL_CATEGORY)
  })

  it('follows the category the template reports', async () => {
    const wrapper = await mountDomains()

    wrapper.findComponent(TemplateDomains).vm.$emit('update:category', 'popular')
    await flushPromises()

    expect(received.props.activeCategory).toBe('popular')
  })

  it('adds a registration to the cart in the payer currency, once', async () => {
    const wrapper = await mountDomains()

    wrapper.findComponent(TemplateDomains).vm.$emit('add', 'example.com')
    wrapper.findComponent(TemplateDomains).vm.$emit('add', 'example.com')
    await flushPromises()

    const items = useCartStore().items
    expect(items).toHaveLength(1)
    expect(items[0]).toMatchObject({
      pid: 7, name: 'example.com', domain: 'example.com', itemType: CartItemType.Domain, domainAction: DomainOrderAction.Register, tld: 'com', years: 1, amount: 10, currency: 'USD',
    })
    expect(received.props.inCart).toEqual(['example.com'])
  })

  it('does not add an extension that is not in the price table', async () => {
    const wrapper = await mountDomains()

    wrapper.findComponent(TemplateDomains).vm.$emit('add', 'example.org')
    await flushPromises()

    expect(useCartStore().items).toEqual([])
  })

  it('adds an extension that sells in another currency, at the payer-currency amount the API converted', async () => {
    table[`GET ${DomainPath.TldPricing}`] = { body: pricing('AMD') }
    const wrapper = await mountDomains()
    useDomainsStore().lookupResults = [{ name: 'example.com', price: '$10.00', status: DomainResultStatus.Available }]
    await flushPromises()

    wrapper.findComponent(TemplateDomains).vm.$emit('add', 'example.com')
    await flushPromises()

    const items = useCartStore().items
    expect(items).toHaveLength(1)
    expect(items[0]).toMatchObject({ domain: 'example.com', amount: 10, currency: 'USD' })
  })

  it('adds nothing when the operator has no domain product', async () => {
    table[`GET ${CatalogPath.Products}`] = { body: [] }
    const wrapper = await mountDomains()

    wrapper.findComponent(TemplateDomains).vm.$emit('add', 'example.com')
    await flushPromises()

    expect(useCartStore().items).toEqual([])
  })

  it('looks the typed name up at once when the address carries it', async () => {
    spies = stubPublicPageRuntime({ query: { [DOMAIN_SEARCH_QUERY_PARAM]: 'example.com' } })

    await mountDomains()
    await until(() => domainChecks().length > 0, 'the typed name to be checked')

    expect(domainChecks()).toHaveLength(1)
    expect(bodyOf(domainChecks()[0])).toEqual({ domain: 'example.com' })
  })

  it('looks nothing up without a name, and leaves the search to the classic template', async () => {
    await mountDomains()
    expect(domainChecks()).toHaveLength(0)

    spies = stubPublicPageRuntime({ query: { [DOMAIN_SEARCH_QUERY_PARAM]: 'example.com' } })
    await mountDomains(TemplateName.Classic)
    expect(domainChecks()).toHaveLength(0)
  })

  it('sets the canonical head and publishes the collection page schema', async () => {
    await mountDomains()

    expect(spies.useSeo).toHaveBeenCalledWith(expect.objectContaining({ path: '/domains' }))
    const schemas = spies.injectSchema.mock.calls[0]?.[0] as Array<Record<string, unknown>>
    expect(schemas.some((schema) => schema['@type'] === 'CollectionPage')).toBe(true)
  })
})
