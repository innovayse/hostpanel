/**
 * Unit tests for the checkout BundleSummary line: the bundle's name, its discount wording and the
 * estimated amount, and nothing when the cart has no bundle or the bundle no discount.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import BundleSummary from '~/components/checkout/BundleSummary.vue'

describe('BundleSummary', () => {
  it('shows the discount wording and the estimated amount taken off', () => {
    const wrapper = mountWithKit(BundleSummary, { props: { name: 'Starter pack', offer: '10% off', discount: '$5.00' } })

    expect(wrapper.find('[data-testid="bundle-summary-offer"]').text()).toBe('10% off')
    expect(wrapper.find('[data-testid="bundle-summary-amount"]').text()).toBe('-$5.00')
  })

  it('renders nothing without a bundle', () => {
    const wrapper = mountWithKit(BundleSummary, { props: { name: '', offer: '', discount: '$0.00' } })

    expect(wrapper.find('[data-testid="bundle-summary-discount"]').exists()).toBe(false)
  })

  it('renders nothing for a bundle that has no discount', () => {
    const wrapper = mountWithKit(BundleSummary, { props: { name: 'Starter pack', offer: '', discount: '$0.00' } })

    expect(wrapper.find('[data-testid="bundle-summary-discount"]').exists()).toBe(false)
  })
})
