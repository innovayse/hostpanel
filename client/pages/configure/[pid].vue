<template>
  <CatalogConfigure
    v-model:selected-cycle="selectedCycle"
    :pending="pending"
    :found="product !== null"
    :group-name="groupName"
    :product-name="productName"
    :description="productDesc"
    :features="features"
    :icon="config?.icon ?? null"
    :color="config?.color ?? null"
    :cycles="cycleOptions"
    :selected-cycle-label="selectedCycleLabel"
    :selected-price="selectedPrice"
    :setup-fee="setupFee"
    @continue="addToCart"
  />
</template>

<script setup lang="ts">
/**
 * Configure page - the billing-cycle choice and order summary for one product.
 * Route: /configure/[pid]
 *
 * The page reads the product through the catalogue store (in the payer's currency, so the read
 * runs again when it or the language changes), formats the prices, and on Continue puts the
 * product in the cart and moves on to checkout. `CatalogConfigure` only renders.
 */
import { PRICING_CYCLES, PRICING_CYCLE_I18N_KEY } from '~/constants/pricingCycle'
import { useCartStore } from '~/stores/cart'
import { useCatalogStore } from '~/stores/catalog'
import { useCurrencyStore } from '~/stores/currency'
import { PricingCycle } from '~/types/pricingcycle'
import type { PortalProduct } from '~/types/portalproduct'
import type { PricingCycleOption } from '~/types/pricingcycleoption'
import { familyConfig, parseDescription } from '~/utils/catalogGroups'
import { formatMoney } from '~/utils/formatMoney'
import { cycleAmount } from '~/utils/productFamilies'

/** The current route. */
const route = useRoute()
/** Translation helpers and the active language. */
const { t, locale } = useI18n()
/** Prefixes a path with the active language. */
const localePath = useLocalePath()
/** The cart: its lines, totals and persistence. */
const cart = useCartStore()
/** The payer's currency and the currencies prices can be shown in. */
const currencyStore = useCurrencyStore()
/** The catalogue: products, plans, slides and TLD prices. */
const catalogStore = useCatalogStore()

onMounted(() => {
  cart.init()
  currencyStore.init()
  currencyStore.load()
})

/** The product id from the address. */
const pid = Number(route.params.pid)

/** Which products the page reads: the one with this id. */
const pidFilter = { pid }

/** Whether the catalogue read is still running. */
const { pending } = await useAsyncData(`configure:${pid}`, async () => {
  await catalogStore.loadProductGroups()
  return catalogStore.loadProducts(pidFilter)
}, {
  watch: [() => currencyStore.payerCode, locale],
})

/** The product, or null when the id matches none. */
const product = computed<PortalProduct | null>(() => catalogStore.productsFor(pidFilter)[0] ?? null)

/** Visual config of the product's family (name, icon, accent colour), or null when its group is unknown. */
const config = computed(() => {
  const group = catalogStore.productGroups.find((candidate) => candidate.id === product.value?.groupId)
  return group ? familyConfig(group, product.value ? [product.value] : []) : null
})

/** Name of the product's family. */
const groupName = computed(() => config.value?.name ?? '')

/** Name of the product. */
const productName = computed(() => product.value?.name ?? '')

/** Summary paragraph and feature lines parsed out of the product description (the copy is HTML). */
const parsedDesc = computed(() => parseDescription(product.value?.description ?? ''))

/** Leading prose of the description, tags stripped; empty for bullet-only copy. */
const productDesc = computed(() => parsedDesc.value.summary)

/** Feature bullets of the description, the only source the API offers. */
const features = computed(() => parsedDesc.value.features)

/** The payer's currency to format and price with, never chosen by the page's language. */
const payerCurrency = computed(() => currencyStore.moneyCurrencyFor(currencyStore.payerCode))

/** The cycles the product is priced for, labelled and formatted. */
const cycleOptions = computed<PricingCycleOption[]>(() =>
  PRICING_CYCLES
    .filter((cycle) => cycleAmount(product.value?.pricing, cycle) !== null)
    .map((cycle) => ({
      cycle,
      label: t(`hosting.cycles.${PRICING_CYCLE_I18N_KEY[cycle]}`),
      price: formatMoney(cycleAmount(product.value?.pricing, cycle), payerCurrency.value),
    })))

/** The cycle the visitor has chosen. */
const selectedCycle = ref<PricingCycle>(PricingCycle.Monthly)

watch(cycleOptions, (options) => {
  const first = options[0]
  if (first && !options.some((option) => option.cycle === selectedCycle.value)) {
    selectedCycle.value = first.cycle
  }
}, { immediate: true })

/** The selected cycle's name in the reader's language. */
const selectedCycleLabel = computed(() => t(`hosting.cycles.${PRICING_CYCLE_I18N_KEY[selectedCycle.value]}`))

/** The selected cycle's price as a number; null when it has none. */
const selectedAmount = computed(() => cycleAmount(product.value?.pricing, selectedCycle.value))

/** The selected cycle's price, formatted; empty when it has none. */
const selectedPrice = computed(() =>
  selectedAmount.value === null ? '' : formatMoney(selectedAmount.value, payerCurrency.value))

/**
 * The setup fee. Always zero: `ProductPricingDto` carries no setup fee and the API has no field
 * for one to come from.
 */
const setupFee = computed(() => formatMoney(0, payerCurrency.value))

/** Adds the configured product to the cart and moves the visitor to checkout. */
const addToCart = (): void => {
  const amount = selectedAmount.value
  if (amount === null || !currencyStore.payerCode) {
    return
  }
  cart.addItem({
    pid,
    name: productName.value,
    billingcycle: selectedCycle.value,
    cycleLabel: selectedCycleLabel.value,
    amount,
    currency: currencyStore.payerCode,
    productType: product.value?.type,
  })
  navigateTo(localePath('/checkout'))
}

useSeo({
  title: `${t('configure.title')} — ${productName.value}`,
  description: productDesc.value,
  path: `/configure/${pid}`,
})
</script>
