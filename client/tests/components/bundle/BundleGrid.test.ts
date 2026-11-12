/**
 * Unit tests for BundleGrid: one card per bundle with what it holds, the discount wording, the
 * domain line and the link into the order screen; nothing at all without a bundle.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import BundleGrid from '~/components/bundle/BundleGrid.vue'
import { BundleDiscountType } from '~/types/bundlediscounttype'
import type { CatalogBundle } from '~/types/catalogbundle'

/**
 * Builds a bundle of two products.
 *
 * @param overrides Fields that differ from the defaults.
 * @returns The bundle.
 */
const bundle = (overrides: Partial<CatalogBundle> = {}): CatalogBundle => ({
  id: 4,
  name: 'Starter pack',
  description: 'Everything for a first site',
  includesDomain: false,
  domainYears: null,
  discountType: BundleDiscountType.Percentage,
  discountAmount: 10,
  discountCurrency: null,
  components: [
    { productId: 11, name: 'Starter hosting', isPrimary: true },
    { productId: 12, name: 'Mailbox', isPrimary: false },
  ],
  ...overrides,
})

/**
 * Mounts the grid.
 *
 * @param bundles The bundles to show.
 * @returns The wrapper.
 */
const mountGrid = (bundles: CatalogBundle[]) =>
  mountWithKit(BundleGrid, { props: { bundles, discountLabel: (offer: CatalogBundle) => (offer.discountAmount > 0 ? `${offer.discountAmount}% off` : '') } })

describe('BundleGrid', () => {
  beforeEach(() => {
    stubPublicPageRuntime()
  })

  it('renders nothing while there is no bundle', () => {
    expect(mountGrid([]).find('[data-testid="bundle-grid"]').exists()).toBe(false)
  })

  it('shows a card per bundle with its products, its discount and the link to its order screen', () => {
    const wrapper = mountGrid([bundle(), bundle({ id: 5, name: 'Pro pack', discountAmount: 0 })])

    expect(wrapper.findAll('[data-testid^="bundle-card-"]')).toHaveLength(2)
    expect(wrapper.find('[data-testid="bundle-card-4"]').text()).toContain('Mailbox')
    expect(wrapper.find('[data-testid="bundle-discount-4"]').text()).toBe('10% off')
    expect(wrapper.find('[data-testid="bundle-discount-5"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="bundle-choose-4"]').attributes('href')).toBe('/en/bundles/4')
  })

  it('names the included domain only for a bundle that has one', () => {
    const wrapper = mountGrid([bundle({ includesDomain: true, domainYears: 2 }), bundle({ id: 5 })])

    expect(wrapper.find('[data-testid="bundle-domain-4"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="bundle-domain-5"]').exists()).toBe(false)
  })
})
