<template>
  <UiStack gap="none" data-testid="cart-page">
    <CartHero />
    <CartContent
      :items="cart.items"
      :stale-count="staleItems.length"
      :total-label="totalLabel"
      :price-of="itemPrice"
      :bundle-name="bundleName"
      :bundle-offer="bundleOffer"
      :bundle-discount="bundleDiscount"
      @remove="removeItem"
      @clear="cart.clear()"
    />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Cart page: the visitor's lines, their total and the way to checkout.
 *
 * The page owns the cart and currency stores; `CartHero` and `CartContent` only render what they
 * are given. The cart lives in localStorage, so it is loaded on mount.
 */
import { useCartBundle } from '~/composables/useCartBundle'
import { useCartStore } from '~/stores/cart'
import { useCurrencyStore } from '~/stores/currency'
import type { CartItem } from '~/types/cartitem'
import { formatMoney } from '~/utils/formatMoney'
import { detailTitle } from '~/utils/seoTitle'

/** Translation helpers and the active language. */
const { t } = useI18n()

/** Site name the operator set; the brand is the fallback. */
const { name: siteName } = useSiteIdentity()
/** The cart: its lines, totals and persistence. */
const cart = useCartStore()
/** The payer's currency and the currencies prices can be shown in. */
const currencyStore = useCurrencyStore()

/** The cart's bundle as display text: its name, its discount in words and the estimated amount taken off. */
const { bundleName, bundleOffer, bundleDiscount } = useCartBundle()

onMounted(() => {
  cart.init()
  currencyStore.init()
  currencyStore.load()
})

/**
 * Formats one cart line's price, in the currency it was actually added in.
 *
 * @param item The cart line.
 * @returns The formatted price.
 */
const itemPrice = (item: CartItem): string =>
  formatMoney(item.amount, currencyStore.moneyCurrencyFor(item.currency))

/** Lines priced in a currency other than the payer's current one. */
const staleItems = computed(() => cart.staleItems(currencyStore.payerCode))

/** Total of the lines priced in the payer's current currency; empty for an empty cart. */
const totalLabel = computed(() => {
  if (!cart.items.length) {
    return ''
  }
  return formatMoney(cart.total(currencyStore.payerCode), currencyStore.moneyCurrencyFor(currencyStore.payerCode))
})

/**
 * Removes one line from the cart.
 *
 * @param item The line the visitor removed.
 */
const removeItem = (item: CartItem): void => {
  cart.removeItem(item.pid, item.billingcycle, item.domain, item.domainAction)
}

useHead({ title: computed(() => detailTitle(t('cart.title'), siteName.value)) })
</script>
