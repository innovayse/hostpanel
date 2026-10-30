/**
 * Unit tests for the configure page: it shows the product named by the address with its cycles and
 * totals, and on Continue puts it in the cart in the chosen cycle and moves on to checkout.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { mountPage, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import ConfigurePage from '~/pages/configure/[pid].vue'
import CatalogConfigure from '~/components/catalog/Configure.vue'
import { useCartStore } from '~/stores/cart'
import type { BillingCurrency } from '~/types/currency'
import type { PortalProduct } from '~/types/portalproduct'
import { ProductType } from '~/types/producttype'

/** The mocked catalogue endpoints, re-created for every test. */
const api = { fetchProducts: vi.fn(), fetchProductGroups: vi.fn() }

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

/** The product the address names. */
const starter: PortalProduct = {
  id: 2, groupId: 1, name: 'Starter', description: 'Small sites<br />✔ 5 GB SSD<br />✔ 10 Emails', pricing: { monthly: 5, annual: 50 },
}

/** The `navigateTo` spy of the current test. */
let navigate: ReturnType<typeof vi.fn>

/**
 * Mounts the page for one product id.
 *
 * @param pid The `pid` route parameter.
 * @returns The wrapper.
 */
const mountConfigure = async (pid = '2') => {
  stubPublicPageRuntime({ params: { pid } })
  const wrapper = await mountPage(ConfigurePage, { components: { CatalogConfigure } })
  await flushPromises()
  return wrapper
}

describe('configure page', () => {
  beforeEach(() => {
    navigate = vi.fn()
    vi.stubGlobal('navigateTo', navigate)
    api.fetchProducts.mockReset()
    api.fetchProducts.mockResolvedValue([starter])
    api.fetchProductGroups.mockReset()
    api.fetchProductGroups.mockResolvedValue([{ id: 1, name: 'Web Hosting', productCount: 1 }])
    window.localStorage.clear()
  })

  it('reads the product by its id', async () => {
    await mountConfigure()

    expect(api.fetchProducts).toHaveBeenCalledWith(expect.objectContaining({ pid: 2 }))
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
    api.fetchProducts.mockResolvedValue([{ ...starter, type: ProductType.Digital }])
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
    api.fetchProducts.mockResolvedValue([])
    const wrapper = await mountConfigure('999')

    expect(wrapper.find('[data-testid="configure-missing"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="configure-continue"]').exists()).toBe(false)
    expect(useCartStore().items).toEqual([])
  })
})
