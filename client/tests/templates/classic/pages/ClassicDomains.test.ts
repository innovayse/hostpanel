/**
 * Unit tests for ClassicDomains against the real catalogue, currency, domains and cart stores, the
 * real API composables and the real transport, with a stub BFF answering on a real socket: the
 * availability search and its three outcomes, adding a registration to the cart, the TLD price table
 * with its category filter and quick search.
 *
 * @module tests/templates/classic/pages/ClassicDomains.test
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { at } from '~/tests/helpers/at'
import { CatalogPath } from '~/tests/helpers/catalogPaths'
import { USD_CURRENCY, awaitCatalogue } from '~/tests/helpers/payerCatalogue'
import { mountPage, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import { bodyOf } from '~/tests/helpers/requestBody'
import { settle, until } from '~/tests/helpers/settle'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import ClassicDomains from '~/templates/classic/pages/ClassicDomains.vue'
import { useCartStore } from '~/stores/cart'
import { useDomainsStore } from '~/stores/domains'
import type { DomainCheckResult } from '~/types/domaincheckresult'
import type { TldPricing } from '~/types/tldpricing'
import { CartItemType } from '~/types/cartitemtype'
import { DomainOrderAction } from '~/types/domainorderaction'
import { DomainAvailabilityStatus } from '~/types/domainavailabilitystatus'
import { ProductType } from '~/types/producttype'

/** The price table the catalogue answers with. */
const pricing: TldPricing = {
  currency: { code: 'USD', prefix: '$' },
  pricing: [
    { tld: 'com', sellCurrency: 'USD', register: [{ years: 1, amount: 10 }], transfer: [{ years: 1, amount: 9 }], renew: [{ years: 1, amount: 11 }], categories: ['popular'] },
    { tld: 'dev', sellCurrency: 'USD', register: [{ years: 1, amount: 15 }], transfer: [{ years: 1, amount: 14 }], renew: [{ years: 1, amount: 16 }], categories: ['technology'] },
  ],
}

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/**
 * Makes the stub answer the availability check with this result.
 *
 * @param result The result the BFF answers every check with.
 */
const answerCheck = (result: DomainCheckResult): void => {
  table[`POST ${CatalogPath.DomainCheck}`] = { body: result }
}

/**
 * The domains the page asked the BFF to check, in order.
 *
 * @returns The checked names.
 */
const checkedDomains = (): string[] =>
  requestsTo(stub.requests, CatalogPath.DomainCheck).map(request => (bodyOf(request) as { domain: string }).domain)

/**
 * Mounts the page and waits until the currencies, prices and domain product have loaded.
 *
 * @param query Route query, e.g. a `domain` to search at once.
 * @returns The wrapper.
 */
const mountDomains = async (query: Record<string, string> = {}) => {
  stubPublicPageRuntime({ query })
  const wrapper = await mountPage(ClassicDomains)
  await awaitCatalogue()
  return wrapper
}

/**
 * Types a domain into the search box, submits it and waits until the check has been answered.
 *
 * @param wrapper The mounted page.
 * @param domain The text to search for.
 */
const search = async (wrapper: Awaited<ReturnType<typeof mountDomains>>, domain: string) => {
  await wrapper.find('[data-testid="search-bar-input"]').setValue(domain)
  await wrapper.find('[data-testid="search-bar-submit"]').trigger('click')
  await until(() => !useDomainsStore().searching, 'the availability check to finish')
  await flushPromises()
}

describe('ClassicDomains', () => {
  beforeEach(async () => {
    window.localStorage.clear()
    table = {
      [`GET ${CatalogPath.BillingCurrencies}`]: { body: [USD_CURRENCY] },
      [`GET ${CatalogPath.TldPricing}`]: { body: pricing },
      [`GET ${CatalogPath.Products}`]: { body: [{ id: 99, type: ProductType.Domain }] },
    }
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('renders the hero heading and the register and transfer tabs', async () => {
    const wrapper = await mountDomains()

    expect(wrapper.find('[data-testid="classic-domains-title"]').text()).toContain('domains.title')
    expect(wrapper.find('[data-testid="classic-domains-tab-transfer"]').attributes('href')).toBe('/en/domains/transfer')
  })

  it('does not search an empty box', async () => {
    const wrapper = await mountDomains()

    await wrapper.find('[data-testid="search-bar-submit"]').trigger('click')
    await settle()

    expect(checkedDomains()).toEqual([])
  })

  it('refuses a name without an extension before asking the server', async () => {
    const wrapper = await mountDomains()

    await search(wrapper, 'example')
    await settle()

    expect(checkedDomains()).toEqual([])
    expect(wrapper.find('[data-testid="classic-domains-error"]').text()).toBe('domains.invalidDomain')
  })

  it('shows an available domain with a register button', async () => {
    answerCheck({ domain: 'example.com', available: true, status: DomainAvailabilityStatus.Available })
    const wrapper = await mountDomains()

    await search(wrapper, 'example.com')

    const result = wrapper.find('[data-testid="classic-domains-availability"]')
    expect(result.text()).toContain('example.com')
    expect(result.text()).toContain('domains.available')
    expect(wrapper.find('[data-testid="classic-domains-register"]').exists()).toBe(true)
  })

  it('adds the registration to the cart at the one-year price', async () => {
    answerCheck({ domain: 'example.com', available: true, status: DomainAvailabilityStatus.Available })
    const wrapper = await mountDomains()
    await search(wrapper, 'example.com')

    await wrapper.find('[data-testid="classic-domains-register"]').trigger('click')

    expect(useCartStore().items[0]).toMatchObject({
      pid: 99,
      name: 'example.com',
      domain: 'example.com',
      itemType: CartItemType.Domain,
      domainAction: DomainOrderAction.Register,
      tld: 'com',
      years: 1,
      amount: 10,
      currency: 'USD',
    })
  })

  it('disables the register button once the domain is in the cart', async () => {
    answerCheck({ domain: 'example.com', available: true, status: DomainAvailabilityStatus.Available })
    const wrapper = await mountDomains()
    await search(wrapper, 'example.com')
    await wrapper.find('[data-testid="classic-domains-register"]').trigger('click')

    const button = wrapper.find('[data-testid="classic-domains-register"]')
    expect(button.attributes('disabled')).toBeDefined()
    expect(button.text()).toBe('domains.alreadyInCart')
  })

  it('offers a transfer for a taken domain', async () => {
    answerCheck({ domain: 'taken.com', available: false, status: DomainAvailabilityStatus.Taken })
    const wrapper = await mountDomains()

    await search(wrapper, 'taken.com')

    expect(wrapper.find('[data-testid="classic-domains-availability"]').text()).toContain('domains.taken')
    expect(wrapper.find('[data-testid="classic-domains-transfer"]').attributes('href')).toBe('/en/domains/transfer?domain=taken.com')
  })

  it('says so when the extension is not offered', async () => {
    answerCheck({ domain: 'example.zzz', available: true, status: DomainAvailabilityStatus.Available })
    const wrapper = await mountDomains()

    await search(wrapper, 'example.zzz')

    expect(wrapper.find('[data-testid="classic-domains-unsupported"]').text()).toContain('domains.tldNotSupported')
    expect(wrapper.find('[data-testid="classic-domains-register"]').exists()).toBe(false)
  })

  it('shows the refusal sentence when the check fails', async () => {
    table[`POST ${CatalogPath.DomainCheck}`] = { status: 400, body: { detail: 'Registry unreachable.' } }
    const wrapper = await mountDomains()

    await search(wrapper, 'example.com')

    expect(wrapper.find('[data-testid="classic-domains-error"]').text()).toBe('Registry unreachable.')
  })

  it('searches at once when the route carries a domain', async () => {
    answerCheck({ domain: 'prefilled.com', available: true, status: DomainAvailabilityStatus.Available })

    await mountDomains({ domain: 'prefilled.com' })
    await until(() => checkedDomains().length > 0, 'the prefilled domain to be checked')

    expect(checkedDomains()).toEqual(['prefilled.com'])
  })

  it('lists every extension of the price table with the one-year prices', async () => {
    const wrapper = await mountDomains()

    const rows = wrapper.findAll('[data-testid="classic-domains-row"]')
    expect(rows).toHaveLength(2)
    expect(at(rows, 0).text()).toContain('.com')
    expect(at(rows, 0).text()).toContain('10.00')
    expect(at(rows, 0).text()).toContain('9.00')
    expect(at(rows, 0).text()).toContain('11.00')
  })

  it('filters the table to a category and back to all', async () => {
    const wrapper = await mountDomains()
    const categories = wrapper.findAll('[data-testid^="classic-domains-category-"]:not([data-testid="classic-domains-category-all"])')
    expect(categories).toHaveLength(2)

    await at(categories, 1).trigger('click')
    expect(wrapper.findAll('[data-testid="classic-domains-row"]').map((row) => row.text())).toEqual([expect.stringContaining('.dev')])

    await wrapper.find('[data-testid="classic-domains-category-all"]').trigger('click')
    expect(wrapper.findAll('[data-testid="classic-domains-row"]')).toHaveLength(2)
  })

  it('runs a quick search for a row, keeping the typed base name', async () => {
    answerCheck({ domain: 'mysite.dev', available: true, status: DomainAvailabilityStatus.Available })
    const wrapper = await mountDomains()
    await wrapper.find('[data-testid="search-bar-input"]').setValue('mysite.com')

    await at(wrapper.findAll('[data-testid="classic-domains-quick-search"]'), 1).trigger('click')
    await until(() => checkedDomains().length > 0, 'the quick search to be checked')

    expect(checkedDomains()).toEqual(['mysite.dev'])
  })

  it('renders the same extensions as cards for small screens', async () => {
    const wrapper = await mountDomains()

    expect(wrapper.findAll('[data-testid="classic-domains-card"]')).toHaveLength(2)
  })

  it('links the two promo boxes', async () => {
    const wrapper = await mountDomains()

    expect(wrapper.find('[data-testid="classic-domains-promo-hosting-link"]').attributes('href')).toBe('/en/hosting')
    expect(wrapper.find('[data-testid="classic-domains-promo-transfer-link"]').attributes('href')).toBe('/en/domains/transfer')
  })

  it('uses classic tokens only: no gradients, blur glows or aurora colour families', async () => {
    const wrapper = await mountDomains()
    const html = wrapper.find('[data-testid="classic-domains"]').html()

    expect(html).not.toMatch(/bg-gradient|blur-\[|animate-blob|shadow-halo|cyan-|violet-|secondary-/)
    expect(html).toContain('classic-')
  })

  it('draws the highlighted title word at the heading size in the brand colour', async () => {
    const wrapper = await mountDomains()

    const highlight = wrapper.find('[data-testid="classic-domains-title"] span')
    expect(highlight.text()).toContain('domains.titleHighlight')
    expect(highlight.classes()).toContain('text-classic-brand')
    expect(highlight.classes()).not.toContain('text-base')
  })

  it('keeps every section in the shared container and never caps the search', async () => {
    const wrapper = await mountDomains()

    expect(wrapper.findAll('.container-custom').length).toBeGreaterThanOrEqual(2)
    expect(wrapper.html()).not.toMatch(/max-w-(2xl|3xl|4xl|5xl|6xl)/)
  })
})
