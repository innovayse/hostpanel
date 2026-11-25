/**
 * Unit tests for the public domains page against the real catalogue, currency, cart and domains
 * stores, API composables and transport, with a stub BFF answering on a real socket (nothing of ours
 * is mocked): it hands the template's component the price table, the lookup state and the cart, adds
 * a registration to the cart only when it can be sold in the payer's currency, and sets the head and
 * the structured data.
 *
 * @module tests/pages/domains/index.test
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { PUBLIC_PAGE_BASE_URL, canonicalOf, mountPage, publishedSchemas, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import type { PublicPageSpies } from '~/tests/helpers/publicPage'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'
import { stubTemplateSlots } from '~/tests/helpers/templateSlots'
import { CatalogPath } from '~/tests/helpers/catalogPaths'
import { USD_CURRENCY, awaitCatalogue } from '~/tests/helpers/payerCatalogue'
import { bodyOf } from '~/tests/helpers/requestBody'
import { requestsTo } from '~/tests/helpers/stubRoutes'
import { until } from '~/tests/helpers/settle'
import DomainsPage from '~/pages/domains/index.vue'
import { useTemplate } from '~/composables/useTemplate'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { useCartStore } from '~/stores/cart'
import { useDomainsStore } from '~/stores/domains'
import { ALL_CATEGORY } from '~/templates/aurora/types/allcategory'
import { DomainResultStatus } from '~/types/domainresultstatus'
import type { TldPricing } from '~/types/tldpricing'
import { CartItemType } from '~/types/cartitemtype'
import { DomainOrderAction } from '~/types/domainorderaction'
import { DOMAIN_SEARCH_QUERY_PARAM } from '~/constants/domainSearchQuery'
import { TemplateName } from '~/types/templatename'
import { TemplateSlot } from '~/types/templateslot'

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
    error: { type: String, default: '' },
  },
  emits: ['search', 'update:category', 'add'],
  setup(props) {
    received.props = props
    return () => h('div', { 'data-testid': 'template-domains' })
  },
})

/** What the stood-in runtime recorded for the current test. */
let spies: PublicPageSpies

/** What the stub answers, by route, and the accessor of the stub the hooks start and close around each test; a test changes the replies before it mounts. */
const { replies, stub } = usePortalSettingsServer({ useTemplate })

/**
 * The domain checks the stub has received.
 *
 * @returns The requests, oldest first.
 */
const domainChecks = () => requestsTo(stub().requests, DomainPath.DomainCheck)

/**
 * Mounts the page and waits until the price table, the catalogue and the currencies have been read.
 *
 * @param template Template the operator chose.
 * @returns The wrapper.
 */
const mountDomains = async (template: TemplateName = TemplateName.Aurora) => {
  await loadPortalSettings(replies, { [PortalSettingKey.Template]: template })
  const wrapper = await mountPage(DomainsPage)
  await awaitCatalogue()
  return wrapper
}

describe('domains page', () => {
  beforeEach(() => {
    spies = stubPublicPageRuntime()
    stubTemplateSlots({ [TemplateSlot.Domains]: TemplateDomains })
    window.localStorage.clear()
    Object.assign(replies, {
      [`GET ${CatalogPath.DomainProduct}`]: { body: { id: 7 } },
      [`GET ${DomainPath.TldPricing}`]: { body: pricing('USD') },
      [`GET ${CatalogPath.BillingCurrencies}`]: { body: [USD_CURRENCY] },
      [`POST ${DomainPath.DomainCheck}`]: { body: { domain: 'example.com', available: true } },
    })
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
    replies[`GET ${DomainPath.TldPricing}`] = { body: pricing('AMD') }
    const wrapper = await mountDomains()
    useDomainsStore().lookupResults = [{ name: 'example.com', price: '$10.00', status: DomainResultStatus.Available }]
    await flushPromises()

    wrapper.findComponent(TemplateDomains).vm.$emit('add', 'example.com')
    await flushPromises()

    const items = useCartStore().items
    expect(items).toHaveLength(1)
    expect(items[0]).toMatchObject({ domain: 'example.com', amount: 10, currency: 'USD' })
  })

  it('adds nothing and shows the API sentence when the operator has no domain product', async () => {
    replies[`GET ${CatalogPath.DomainProduct}`] = { status: 503, body: { type: 'DOMAIN_PRODUCT_NOT_CONFIGURED', detail: 'Domains cannot be ordered here.' } }
    const wrapper = await mountDomains()

    wrapper.findComponent(TemplateDomains).vm.$emit('add', 'example.com')
    await flushPromises()

    expect(useCartStore().items).toEqual([])
    expect(received.props.error).toBe('Domains cannot be ordered here.')
  })

  it('finds the domain product by its type and never reads the catalogue for it', async () => {
    await mountDomains()

    expect(requestsTo(stub().requests, CatalogPath.DomainProduct)).toHaveLength(1)
    expect(requestsTo(stub().requests, CatalogPath.Products)).toHaveLength(0)
  })

  it('tells the visitor when an extension has no price instead of doing nothing, and clears it on the next search', async () => {
    const wrapper = await mountDomains()

    wrapper.findComponent(TemplateDomains).vm.$emit('add', 'example.org')
    await flushPromises()
    expect(received.props.error).toBe('domains.cannotAdd')

    wrapper.findComponent(TemplateDomains).vm.$emit('search', 'example')
    await flushPromises()
    expect(received.props.error).toBe('')
  })

  it('hands the template the sentence of a malformed name at once, without asking the API', async () => {
    const wrapper = await mountDomains()

    wrapper.findComponent(TemplateDomains).vm.$emit('search', 'bad name!')
    await flushPromises()

    expect(received.props.error).toBe('domains.invalidName')
    expect(received.props.results).toEqual([])
    expect(domainChecks()).toHaveLength(0)
  })

  it('hands the template the sentence of a name the API refused', async () => {
    replies[`POST ${DomainPath.DomainCheck}`] = { status: 400, body: { type: 'VALIDATION_ERROR', errors: { Domain: ['Domain name is reserved.'] } } }
    const wrapper = await mountDomains()

    wrapper.findComponent(TemplateDomains).vm.$emit('search', 'reserved')
    await until(() => received.props.error === 'Domain name is reserved.', 'the refusal to reach the template')

    expect(received.props.results).toEqual([])
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

    expect(canonicalOf(spies)).toBe(`${PUBLIC_PAGE_BASE_URL}/domains`)
    expect(publishedSchemas(spies).some((schema) => schema['@type'] === 'CollectionPage')).toBe(true)
  })
})
