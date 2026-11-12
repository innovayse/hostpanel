/**
 * Unit tests for BundleConfigure: the loading and missing states, a cycle choice per component, the
 * summary with the estimated discount and total, and the Continue button that waits for a complete
 * bundle.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import BundleConfigure from '~/components/bundle/BundleConfigure.vue'
import CheckoutBundleSummary from '~/components/checkout/BundleSummary.vue'
import type { BundleComponentRow } from '~/types/bundlecomponentrow'
import { BundleDiscountType } from '~/types/bundlediscounttype'
import type { CatalogBundle } from '~/types/catalogbundle'
import { PricingCycle } from '~/types/pricingcycle'

/** A bundle of one product. */
const bundle: CatalogBundle = {
  id: 4,
  name: 'Starter pack',
  description: null,
  includesDomain: false,
  domainYears: null,
  discountType: BundleDiscountType.Percentage,
  discountAmount: 10,
  discountCurrency: null,
  components: [{ productId: 11, name: 'Starter hosting', isPrimary: true }],
}

/** The cycle the row has chosen. */
const monthly = { cycle: PricingCycle.Monthly, label: 'Monthly', amount: 10, price: '$10.00' }

/** The product, priced monthly and yearly, on the monthly cycle. */
const row: BundleComponentRow = {
  productId: 11,
  name: 'Starter hosting',
  isPrimary: true,
  options: [monthly, { cycle: PricingCycle.Annual, label: 'Annually', amount: 100, price: '$100.00' }],
  selected: monthly,
}

/**
 * Mounts the screen.
 *
 * @param props Props that differ from the defaults.
 * @returns The wrapper.
 */
const mountScreen = (props: Record<string, unknown> = {}) =>
  mountWithKit(BundleConfigure, {
    props: {
      pending: false,
      bundle,
      rows: [row],
      discountLabel: '10% off',
      lines: [{ id: 'product-11', name: 'Starter hosting', detail: 'Monthly', price: '$10.00' }],
      discount: '$1.00',
      total: '$9.00',
      complete: true,
      ...props,
    },
    components: { CheckoutBundleSummary },
  })

describe('BundleConfigure', () => {
  beforeEach(() => {
    stubPublicPageRuntime()
  })

  it('shows a skeleton while the bundle is read', () => {
    const wrapper = mountScreen({ pending: true })

    expect(wrapper.find('[data-testid="bundle-loading"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="bundle-name"]').exists()).toBe(false)
  })

  it('tells the visitor when no bundle matches', () => {
    const wrapper = mountScreen({ bundle: null })

    expect(wrapper.find('[data-testid="bundle-missing"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="bundle-missing-back"]').attributes('href')).toBe('/en/products')
  })

  it('shows the bundle, its discount and a cycle choice for each component', () => {
    const wrapper = mountScreen()

    expect(wrapper.find('[data-testid="bundle-name"]').text()).toBe('Starter pack')
    expect(wrapper.find('[data-testid="bundle-discount"]').text()).toBe('10% off')
    expect(wrapper.find('[data-testid="bundle-component-11"]').text()).toContain('Starter hosting')
    expect(wrapper.find('[data-testid="bundle-cycle-11"]').exists()).toBe(true)
  })

  it('warns instead of offering a choice for a product without a price', () => {
    const wrapper = mountScreen({ rows: [{ ...row, options: [], selected: null }], complete: false })

    expect(wrapper.find('[data-testid="bundle-unpriced-11"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="bundle-cycle-11"]').exists()).toBe(false)
  })

  it('lists the priced lines and the estimated discount and total', () => {
    const wrapper = mountScreen()

    expect(wrapper.findAll('[data-testid="bundle-line"]')).toHaveLength(1)
    expect(wrapper.find('[data-testid="bundle-summary-amount"]').text()).toBe('-$1.00')
    expect(wrapper.find('[data-testid="bundle-total"]').text()).toBe('$9.00')
  })

  it('leaves the discount line out for a bundle without one', () => {
    const wrapper = mountScreen({ discountLabel: '' })

    expect(wrapper.find('[data-testid="bundle-summary-discount"]').exists()).toBe(false)
  })

  it('reports Continue only when the bundle is complete', async () => {
    const incomplete = mountScreen({ complete: false })
    expect(incomplete.find('[data-testid="bundle-continue"]').attributes('disabled')).toBeDefined()
    expect(incomplete.find('[data-testid="bundle-incomplete"]').exists()).toBe(true)

    const complete = mountScreen()
    await complete.find('[data-testid="bundle-continue"]').trigger('click')

    expect(complete.emitted('continue')).toHaveLength(1)
  })

  it('renders the domain step the page puts in the domain slot', () => {
    const wrapper = mountWithKit(BundleConfigure, {
      props: { pending: false, bundle, rows: [row], discountLabel: '', lines: [], discount: '', total: '$10.00', complete: false },
      slots: { domain: '<div data-testid="domain-slot" />' },
      components: { CheckoutBundleSummary },
    })

    expect(wrapper.find('[data-testid="domain-slot"]').exists()).toBe(true)
  })
})
