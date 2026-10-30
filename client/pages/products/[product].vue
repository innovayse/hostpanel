<template>
  <UiStack v-if="productData" gap="none" data-testid="product-page">
    <CatalogProductDetail
      v-model:selected-cycle="selectedCycle"
      :product="productData"
      :cycles="availableCycles"
      :price-for="priceFor"
      :is-in-cart="isInCart"
      :trial-url-for="planCtaUrl"
      :comparison-rows="comparisonRows"
      @add-to-cart="addPlanToCart"
      @trial="handlePricingCta"
    >
      <template #breadcrumbs>
        <UiBreadcrumbs :items="breadcrumbItems" class="mb-6" />
      </template>
      <template #faq>
        <SectionsFAQ :limit="FAQ_PREVIEW_LIMIT" :show-categories="false" />
      </template>
    </CatalogProductDetail>
  </UiStack>

  <UiStack v-else align="center" justify="center" class="min-h-[60dvh] bg-ui-page" data-testid="product-not-found">
    <!-- Not found -->
    <UiEmptyState :icon="Package" :title="$t('products.notFound')" :description="$t('products.notFoundDescription')">
      <UiButton :to="localePath('/products')" icon="mdi:arrow-left" data-testid="product-back">
        {{ $t('common.backToProducts') }}
      </UiButton>
    </UiEmptyState>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Product detail page - one product family with its plans, prices and calls to action.
 * Route: /products/[product]
 *
 * The page reads the catalogue through the catalogue store (prices are in the payer's currency, so
 * the read runs again when it or the language changes) and the family's specification lines, finds
 * the family with `findProductDetail` and hands it to `CatalogProductDetail`. It owns the cart, the
 * external order links, the head and the family's structured data (a `Product` with an `Offer` per plan).
 */
import { Package } from 'lucide-vue-next'
import { PRICING_CYCLES, PRICING_CYCLE_I18N_KEY, PRICING_CYCLE_ISO_DURATION } from '~/constants/pricingCycle'
import { useCartStore } from '~/stores/cart'
import { useCatalogStore } from '~/stores/catalog'
import { useCurrencyStore } from '~/stores/currency'
import { PricingCycle } from '~/types/pricingcycle'
import type { ProductPlan } from '~/types/productplan'
import { buildComparisonRows } from '~/utils/comparisonRows'
import { formatMoney } from '~/utils/formatMoney'
import { findProductDetail, firstOfferedPrice } from '~/utils/productFamilies'
import { buildSeoLinks, localizedUrl } from '~/utils/seoLinks'
import { shareImageMeta } from '~/utils/shareImage'

/** Translation helpers and the active language. */
const { t, locale } = useI18n()

/** Site name the operator set; the brand is the fallback. */
const { name: siteName } = useSiteIdentity()
/** Prefixes a path with the active language. */
const localePath = useLocalePath()
/** The current route. */
const route = useRoute()
/** Schema.org builders and the head injector. */
const { productFamilySchema, breadcrumbSchema, injectSchema } = useSchemaOrg()
/** The cart: its lines, totals and persistence. */
const cart = useCartStore()
/** The payer's currency and the currencies prices can be shown in. */
const currencyStore = useCurrencyStore()
/** The catalogue: products, plans, slides and TLD prices. */
const catalogStore = useCatalogStore()

/** Public site address the canonical and schema URLs are built from. */
const baseUrl = useRuntimeConfig().public.baseUrl

/** Route param, e.g. `alpha-suite`. */
const productId = route.params.product as string

/** How many questions the page's FAQ block previews before it links to the full FAQ. */
const FAQ_PREVIEW_LIMIT = 5

/** The family the route names, shaped for the page; null when no product matches. */
const productData = computed(() =>
  findProductDetail(catalogStore.familyGroups, catalogStore.familyProducts, productId, catalogStore.featuresFor))

await useAsyncData(`product:${productId}`, async () => {
  const list = await catalogStore.loadFamilies()
  // The specification lines back the key bullets and the comparison of the family's plans.
  const family = findProductDetail(catalogStore.familyGroups, catalogStore.familyProducts, productId)
  if (family) {
    await catalogStore.loadProductFeatures(family.gid)
  }
  return list
}, {
  watch: [() => currencyStore.payerCode, locale],
})

/** The specification rows of the family's plans, aligned to the plan cards; the page shows them when they are comparable. */
const comparisonRows = computed(() => {
  const family = productData.value
  return family ? buildComparisonRows(catalogStore.featuresFor(family.gid), family.plans.map((plan) => plan.id)) : []
})

/** The cycles at least one plan is priced for. */
const availableCycles = computed<PricingCycle[]>(() =>
  PRICING_CYCLES.filter((cycle) => productData.value?.plans.some((plan) => plan.pricing?.[cycle] !== null && plan.pricing?.[cycle] !== undefined)))

/** The cycle the prices are shown for. */
const selectedCycle = ref<PricingCycle>(PricingCycle.Monthly)

watch(availableCycles, (cycles) => {
  const first = cycles[0]
  if (first && !cycles.includes(selectedCycle.value)) {
    selectedCycle.value = first
  }
}, { immediate: true })

/** The payer's currency to format prices with, never chosen by the page's language. */
const payerCurrency = computed(() => currencyStore.moneyCurrencyFor(currencyStore.payerCode))

/**
 * The price of a plan for the selected cycle.
 *
 * @param plan The plan.
 * @returns The formatted price, or an empty string when the plan has none for that cycle.
 */
const priceFor = (plan: ProductPlan): string => {
  const amount = plan.pricing?.[selectedCycle.value]
  if (amount === null || amount === undefined) {
    return ''
  }
  return formatMoney(amount, payerCurrency.value)
}

/**
 * Whether a plan is already in the cart for the selected cycle.
 *
 * @param plan The plan.
 * @returns True when it is.
 */
const isInCart = (plan: ProductPlan): boolean => cart.hasItem(plan.id, selectedCycle.value)

/** Breadcrumb steps: the products list, then this product. */
const breadcrumbItems = computed(() => [
  { label: t('common.products'), to: localePath('/products') },
  { label: productData.value?.name || '' },
])

onMounted(() => {
  cart.init()
  currencyStore.init()
  currencyStore.load()
})

/**
 * Base URL of the billing-portal instance this deployment fronts, or an empty string.
 *
 * Empty is a legitimate configuration, so every link built from it is guarded.
 */
const billingPortalUrl = useRuntimeConfig().public.billingPortalUrl

/**
 * Resolves the external "buy / trial" destination for a plan.
 *
 * With no billing portal configured the plan has no external order page, and an unguarded
 * fallback would open a path on this app's own origin.
 *
 * @param plan The plan being rendered.
 * @returns The absolute destination, or null when this deployment has none.
 */
const planCtaUrl = (plan: ProductPlan): string | null => {
  if (!billingPortalUrl) {
    return null
  }
  return `${billingPortalUrl}/cart.php?a=add&pid=${plan.id}&billingcycle=${selectedCycle.value}`
}

/**
 * Opens a plan's external order page in a new tab.
 *
 * @param plan The plan the visitor clicked.
 */
const handlePricingCta = (plan: ProductPlan): void => {
  const url = planCtaUrl(plan)
  if (url) {
    window.open(url, '_blank')
  }
}

/**
 * Adds a plan to the cart with the selected billing cycle.
 *
 * @param plan The plan the visitor chose.
 */
const addPlanToCart = (plan: ProductPlan): void => {
  const cycle = selectedCycle.value
  const amount = plan.pricing?.[cycle]
  if (amount === null || amount === undefined || !currencyStore.payerCode) {
    return
  }
  cart.addItem({
    pid: plan.id,
    name: plan.tier,
    billingcycle: cycle,
    cycleLabel: t(`hosting.cycles.${PRICING_CYCLE_I18N_KEY[cycle]}`),
    amount,
    currency: currencyStore.payerCode,
    productType: plan.type,
  })
}

/** Path of this page, without the language prefix. */
const productPath = computed(() => `/products/${productId}`)

/** Canonical address of this page in the current language. */
const productCanonical = computed(() => localizedUrl(baseUrl, productPath.value, locale.value))

/**
 * One offer per plan that has a price, for the family's structured data.
 *
 * Each plan is offered at the first cycle it is priced for (monthly when it has one), in the payer's
 * currency exactly as the catalogue sent it; nothing is published while the currency is unresolved.
 */
const familyOffers = computed(() => {
  const currency = currencyStore.payerCode
  if (!currency) {
    return []
  }
  return (productData.value?.plans ?? []).flatMap((plan) => {
    const offered = firstOfferedPrice(plan.pricing)
    return offered
      ? [{ name: plan.tier, price: offered.amount, priceCurrency: currency, billingDuration: PRICING_CYCLE_ISO_DURATION[offered.cycle], url: productCanonical.value }]
      : []
  })
})

watchEffect(() => {
  const product = productData.value
  if (!product) {
    return
  }
  const fullTitle = `${product.name} | ${siteName.value}`
  // A few catalogue products carry no description; fall back to the catalogue-wide copy.
  const metaDescription = product.description || t('seo.products.description')

  useSeoMeta({
    title: fullTitle,
    description: metaDescription,
    ogTitle: fullTitle,
    ogDescription: metaDescription,
    ...shareImageMeta(baseUrl, fullTitle),
    ogType: 'website',
    ogUrl: productCanonical.value,
    twitterCard: 'summary_large_image',
    twitterTitle: fullTitle,
    twitterDescription: metaDescription,
  })

  injectSchema([
    productFamilySchema({
      name: product.name,
      description: metaDescription,
      url: productCanonical.value,
      offers: familyOffers.value,
    }),
    breadcrumbSchema([
      { name: 'Home', url: baseUrl },
      { name: t('common.products'), url: `${baseUrl}/products` },
      { name: product.name, url: productCanonical.value },
    ]),
  ])
})

useHead(() => ({
  link: buildSeoLinks(baseUrl, productPath.value, locale.value),
}))
</script>
