/**
 * Tests for `composables/useBundlePage.ts` with the real stores, API composables and transport against a stub BFF: the
 * bundle and its priced rows, the cycle and domain choices, the estimated total, and the cart
 * hand-off on Continue.
 *
 * @module tests/composables/useBundlePage.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import { mountPage, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import { CatalogPath } from '~/tests/helpers/catalogPaths'
import { USD_CURRENCY, awaitCatalogue } from '~/tests/helpers/payerCatalogue'
import { readOf, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { useBundlePage } from '~/composables/useBundlePage'
import { useCartStore } from '~/stores/cart'
import { BundleDiscountType } from '~/types/bundlediscounttype'
import type { CatalogBundle } from '~/types/catalogbundle'
import { DomainOrderAction } from '~/types/domainorderaction'
import { PricingCycle } from '~/types/pricingcycle'
import type { TldPricing } from '~/types/tldpricing'

/** A bundle of one hosting plan, with a 10 percent discount. */
const baseBundle: CatalogBundle = {
  id: 4,
  name: 'Starter pack',
  description: null,
  includesDomain: false,
  domainYears: null,
  discountType: BundleDiscountType.Percentage,
  discountAmount: 10,
  discountCurrency: null,
  components: [{ productId: 11, name: 'Starter', isPrimary: true }],
}

/** The hosting plan and the domain product of the catalogue. */
const catalogue = [
  { id: 11, groupId: 1, name: 'Starter', pricing: { monthly: 10, annual: 100 } },
  { id: 7, groupId: 2, name: 'Domain Registration' },
]

/** A price table with one extension. */
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
 * Runs the composable the way a page does: inside a component's async setup, under `Suspense`.
 *
 * @param id The bundle id.
 * @returns What the composable returned.
 */
const runInPage = async (id = 4) => {
  /** The composable's result, captured from inside the host's setup. */
  let page: Awaited<ReturnType<typeof useBundlePage>> | undefined
  const Host = defineComponent({
    /**
     * Calls the composable.
     *
     * @returns An empty render function.
     */
    async setup() {
      page = await useBundlePage(id)
      return () => h('div')
    },
  })
  stubPublicPageRuntime()
  await mountPage(Host)
  await awaitCatalogue()
  if (!page) {
    throw new Error('the composable did not run')
  }
  return page
}

describe('useBundlePage', () => {
  beforeEach(async () => {
    navigate = vi.fn()
    vi.stubGlobal('navigateTo', navigate)
    table = {
      [readOf(CatalogPath.BillingCurrencies)]: { body: [USD_CURRENCY] },
      [readOf(CatalogPath.Products)]: { body: catalogue },
      [readOf(CatalogPath.ProductBundles)]: { body: [baseBundle] },
      [readOf(CatalogPath.TldPricing)]: { body: pricing },
    }
    stub = await startTransport(routeTable(table))
    window.localStorage.clear()
  })

  afterEach(async () => {
    await stub.close()
  })

  it('prices the rows on the first cycle and estimates the total after the discount', async () => {
    const page = await runInPage()

    expect(page.bundle.value?.name).toBe('Starter pack')
    expect(page.rows.value[0]?.selected?.cycle).toBe(PricingCycle.Monthly)
    expect(page.totalText.value).toContain('9.00')
    expect(page.discountText.value).toContain('1.00')
  })

  it('reprices when another cycle is chosen', async () => {
    const page = await runInPage()

    page.chooseCycle(11, PricingCycle.Annual)

    expect(page.rows.value[0]?.selected?.amount).toBe(100)
    expect(page.lines.value).toHaveLength(1)
  })

  it('finds no bundle for an unknown id', async () => {
    const page = await runInPage(99)

    expect(page.bundle.value).toBeNull()
    expect(page.complete.value).toBe(false)
  })

  it('refuses a malformed name and an extension that is not priced', async () => {
    table[readOf(CatalogPath.ProductBundles)] = { body: [{ ...baseBundle, includesDomain: true, domainYears: 1 }] }
    const page = await runInPage()

    page.chooseDomain({ action: DomainOrderAction.Register, name: 'example', eppCode: '' })
    expect(page.domainError.value).toBe('domains.invalidDomain')
    page.chooseDomain({ action: DomainOrderAction.Register, name: 'example.org', eppCode: '' })
    expect(page.domainError.value).toBe('domains.tldNotSupported')
    expect(page.domainChoice.value).toBeNull()
  })

  it('accepts a priced domain, adds its line for the bundle years and lets it be cleared', async () => {
    table[readOf(CatalogPath.ProductBundles)] = { body: [{ ...baseBundle, includesDomain: true, domainYears: 2 }] }
    const page = await runInPage()

    page.chooseDomain({ action: DomainOrderAction.Register, name: 'example.com', eppCode: '' })

    expect(page.lines.value.map(line => line.id)).toEqual(['product-11', 'domain'])
    expect(page.complete.value).toBe(true)
    page.clearDomain()
    expect(page.domainChoice.value).toBeNull()
    expect(page.complete.value).toBe(false)
  })

  it('fills the cart and moves on to checkout on Continue', async () => {
    const page = await runInPage()

    page.addToCart()

    const cart = useCartStore()
    expect(cart.bundle).toMatchObject({ id: 4 })
    expect(cart.items[0]).toMatchObject({ pid: 11, bundleId: 4 })
    expect(navigate).toHaveBeenCalledWith('/en/checkout')
  })
})
