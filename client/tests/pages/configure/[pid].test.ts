/**
 * Unit tests for the configure page against the real catalogue, currency and cart stores, API
 * composables and transport, with a stub BFF answering on a real socket (nothing of ours is mocked):
 * it shows the product named by the address with its cycles and totals, and on Continue puts it in
 * the cart in the chosen cycle and moves on to checkout.
 *
 * @module tests/pages/configure/[pid].test
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountPage, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import { CatalogPath } from '~/tests/helpers/catalogPaths'
import { USD_CURRENCY, awaitCatalogue } from '~/tests/helpers/payerCatalogue'
import { queryOf, requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import ConfigurePage from '~/pages/configure/[pid].vue'
import CatalogConfigure from '~/components/catalog/Configure.vue'
import { useCartStore } from '~/stores/cart'
import type { PortalProduct } from '~/types/portalproduct'
import { ProductType } from '~/types/producttype'

/** The product the address names. */
const starter: PortalProduct = {
  id: 2, groupId: 1, name: 'Starter', description: 'Small sites<br />✔ 5 GB SSD<br />✔ 10 Emails', pricing: { monthly: 5, annual: 50 },
}

/** The `navigateTo` spy of the current test. */
let navigate: ReturnType<typeof vi.fn>

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/**
 * Mounts the page for one product id and waits until the catalogue has been read in the payer currency.
 *
 * @param pid The `pid` route parameter.
 * @returns The wrapper.
 */
const mountConfigure = async (pid = '2') => {
  stubPublicPageRuntime({ params: { pid } })
  const wrapper = await mountPage(ConfigurePage, { components: { CatalogConfigure } })
  await awaitCatalogue()
  return wrapper
}

describe('configure page', () => {
  beforeEach(async () => {
    navigate = vi.fn()
    vi.stubGlobal('navigateTo', navigate)
    window.localStorage.clear()
    table = {
      [`GET ${CatalogPath.Products}`]: { body: [starter] },
      [`GET ${CatalogPath.ProductGroups}`]: { body: [{ id: 1, name: 'Web Hosting', productCount: 1 }] },
      [`GET ${CatalogPath.BillingCurrencies}`]: { body: [USD_CURRENCY] },
    }
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('reads the product by its id', async () => {
    await mountConfigure()

    const reads = requestsTo(stub.requests, CatalogPath.Products)
    expect(reads.length).toBeGreaterThan(0)
    expect(reads.map(queryOf)).toEqual(reads.map(() => expect.objectContaining({ pid: '2' })))
  })

  it('shows the product, its family and its features', async () => {
    const wrapper = await mountConfigure()

    expect(wrapper.find('[data-testid="configure-product-name"]').text()).toBe('Starter')
    expect(wrapper.text()).toContain('Web Hosting')
    expect(wrapper.find('[data-testid="configure-features"]').text()).toContain('10 Emails')
  })

  it('totals the monthly price first and the annual one after the visitor switches', async () => {
    const wrapper = await mountConfigure()
    expect(wrapper.find('[data-testid="configure-total"]').text()).toContain('5.00')

    await wrapper.find('[data-testid="configure-cycle-annual"]').trigger('click')

    expect(wrapper.find('[data-testid="configure-total"]').text()).toContain('50.00')
  })

  it('keeps the product type on the cart line, so the order of a digital good offers no hosting setup', async () => {
    table[`GET ${CatalogPath.Products}`] = { body: [{ ...starter, type: ProductType.Digital }] }
    const wrapper = await mountConfigure()

    await wrapper.find('[data-testid="configure-continue"]').trigger('click')

    expect(useCartStore().items[0]?.productType).toBe(ProductType.Digital)
  })

  it('puts the product in the cart in the chosen cycle and moves on to checkout', async () => {
    const wrapper = await mountConfigure()
    await wrapper.find('[data-testid="configure-cycle-annual"]').trigger('click')

    await wrapper.find('[data-testid="configure-continue"]').trigger('click')

    expect(useCartStore().items[0]).toMatchObject({ pid: 2, name: 'Starter', billingcycle: 'annual', amount: 50, currency: 'USD' })
    expect(navigate).toHaveBeenCalledWith('/en/checkout')
  })

  it('answers an unknown product with a notice and does not add anything', async () => {
    table[`GET ${CatalogPath.Products}`] = { body: [] }
    const wrapper = await mountConfigure('999')

    expect(wrapper.find('[data-testid="configure-missing"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="configure-continue"]').exists()).toBe(false)
    expect(useCartStore().items).toEqual([])
  })
})
