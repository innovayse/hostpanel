/**
 * What the screens that total the cart say about its bundle: the name, the discount in words and
 * the estimated amount taken off, in the payer's currency.
 *
 * The cart, the cart drawer and both checkout templates show the same bundle line; this is where
 * it is worded and formatted once.
 *
 * @module composables/useCartBundle
 */

import { useBundleDiscountLabel } from '~/composables/useBundleDiscountLabel'
import { useCartStore } from '~/stores/cart'
import { useCurrencyStore } from '~/stores/currency'
import { formatMoney } from '~/utils/formatMoney'

/**
 * Reads the cart's bundle as display text.
 *
 * @returns The bundle's name and discount wording (both empty without a bundle) and the formatted
 * estimated discount.
 */
export const useCartBundle = () => {
  const cart = useCartStore()
  const currencyStore = useCurrencyStore()
  const discountLabel = useBundleDiscountLabel()

  /** The bundle's name; empty when the cart holds no bundle. */
  const bundleName = computed(() => cart.bundle?.name ?? '')

  /** The bundle's discount in words; empty without a bundle or without a discount. */
  const bundleOffer = computed(() => (cart.bundle ? discountLabel(cart.bundle) : ''))

  /** The estimated discount in the payer's currency, formatted. */
  const bundleDiscount = computed(() =>
    formatMoney(cart.discount(currencyStore.payerCode), currencyStore.moneyCurrencyFor(currencyStore.payerCode)))

  return { bundleName, bundleOffer, bundleDiscount }
}
