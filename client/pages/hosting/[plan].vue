<template>
  <UiStack gap="none" data-testid="hosting-plan-page">
    <CatalogHostingPlanHero
      :pending="pending"
      :plan="currentPlan"
      :summary="planSummary"
      :price="planPrice"
      :in-cart="inCart"
      @order="addToCart"
    />

    <CatalogHostingPlanDetails
      v-if="currentPlan"
      v-model:selected-cycle="selectedCycle"
      :cycles="cycleOptions"
      :features="planFeatures"
      :in-cart="inCart"
      @order="addToCart"
    />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Hosting plan page - one plan with its price, billing cycles and included features.
 * Route: /hosting/[plan]
 *
 * The plan is found among the hosting products by id, slug or normalised name. The page owns the
 * data, the selected cycle, the cart and the head; `CatalogHostingPlanHero` and
 * `CatalogHostingPlanDetails` only render what they are given.
 */
import { PRICING_CYCLES, PRICING_CYCLE_I18N_KEY } from '~/constants/pricingCycle'
import { HOSTING_GROUP_ID } from '~/constants/catalogGroup'
import { usePayerAsyncData } from '~/composables/usePayerAsyncData'
import { useCartStore } from '~/stores/cart'
import { useCatalogStore } from '~/stores/catalog'
import { useCurrencyStore } from '~/stores/currency'
import { PricingCycle } from '~/types/pricingcycle'
import type { PortalProduct } from '~/types/portalproduct'
import type { PricingCycleOption } from '~/types/pricingcycleoption'
import { parseDescription } from '~/utils/catalogGroups'
import { formatMoney } from '~/utils/formatMoney'
import { isFreePlan } from '~/utils/isFreePlan'
import { cycleAmount } from '~/utils/productFamilies'
import { buildSeoLinks } from '~/utils/seoLinks'

/** Translation helpers and the active language. */
const { t, locale } = useI18n()

/** Site name the operator set; the brand is the fallback. */
const { name: siteName } = useSiteIdentity()
/** The current route. */
const route = useRoute()
/** The cart: its lines, totals and persistence. */
const cart = useCartStore()
/** The payer's currency and the currencies prices can be shown in. */
const currencyStore = useCurrencyStore()
/** The catalogue: products, plans, slides and TLD prices. */
const catalogStore = useCatalogStore()

onMounted(() => {
  currencyStore.init()
  currencyStore.load()
})

/** The payer's currency to format and price with, never chosen by the page's language. */
const payerCurrency = computed(() => currencyStore.moneyCurrencyFor(currencyStore.payerCode))

/** Which products the page reads: the hosting group. */
const hostingFilter = { gid: HOSTING_GROUP_ID }

/** Whether the catalogue read is still running. */
const { pending } = await usePayerAsyncData('hosting:plan-products', () => catalogStore.loadProducts(hostingFilter), {
  watch: [locale],
})

/** Every hosting plan, in the payer's currency. */
const plans = computed(() => catalogStore.productsFor(hostingFilter))

/** The billing cycle the visitor has chosen; the backend prices only these two. */
const selectedCycle = ref<PricingCycle>(PricingCycle.Monthly)

/**
 * Normalises a plan name to the slug its address uses ("Businnes Plus" becomes "business-plus").
 *
 * @param name The plan name.
 * @returns The slug.
 */
const slugOf = (name: string): string =>
  (name || '')
    .replace(/Businnes/g, 'Business')
    .replace(/businnes/g, 'business')
    .toLowerCase()
    .replace(/\s+/g, '-')
    .replace(/[^a-z0-9-]/g, '')

/** The address parameter of the page. */
const planParam = computed(() => String(route.params.plan))

/** The plan the address names, or null when there is none. */
const currentPlan = computed<PortalProduct | null>(() =>
  plans.value.find((plan) => String(plan.id) === planParam.value || plan.slug === planParam.value || slugOf(plan.name) === planParam.value) ?? null)

/** Summary paragraph and feature lines parsed out of the plan's description (the copy is HTML). */
const parsedPlan = computed(() => parseDescription(currentPlan.value?.description || ''))

/** Leading prose of the plan description; empty for plans whose copy is bullets only. */
const planSummary = computed(() => parsedPlan.value.summary)

/** Bullet lines of the plan description, markers stripped. */
const planFeatures = computed(() => parsedPlan.value.features)

/** The headline price: the selected cycle's, else the monthly one, else "custom". */
const planPrice = computed(() => {
  const plan = currentPlan.value
  const amount = plan ? (cycleAmount(plan.pricing, selectedCycle.value) ?? cycleAmount(plan.pricing, PricingCycle.Monthly)) : null
  return amount === null ? t('hosting.custom') : formatMoney(amount, payerCurrency.value)
})

/** Whether the current plan is free (zero monthly price, as in the catalogue store). */
const isFree = computed(() => (currentPlan.value ? isFreePlan(currentPlan.value.pricing) : false))

/** The cycle the plan goes into the cart on: monthly for a free plan, the selected one otherwise. */
const cartCycle = computed<PricingCycle>(() => (isFree.value ? PricingCycle.Monthly : selectedCycle.value))

/**
 * The cycles the current plan is priced for, labelled and formatted. A free plan offers only its one
 * monthly, no-charge choice, so no other cycle is shown that would silently become monthly in the cart.
 */
const cycleOptions = computed<PricingCycleOption[]>(() => {
  const plan = currentPlan.value
  if (!plan?.pricing) {
    return []
  }
  if (isFree.value) {
    return [{ cycle: PricingCycle.Monthly, label: t('hosting.cycles.free'), price: formatMoney(0, payerCurrency.value) }]
  }
  return PRICING_CYCLES
    .filter((cycle) => cycleAmount(plan.pricing, cycle) !== null)
    .map((cycle) => ({
      cycle,
      label: t(`hosting.cycles.${PRICING_CYCLE_I18N_KEY[cycle]}`),
      price: formatMoney(cycleAmount(plan.pricing, cycle), payerCurrency.value),
    }))
})

watch(cycleOptions, (options) => {
  const first = options[0]
  if (first && !options.some((option) => option.cycle === selectedCycle.value)) {
    selectedCycle.value = first.cycle
  }
}, { immediate: true })

/** Whether the current plan is already in the cart on the cycle it is added with. */
const inCart = computed(() => (currentPlan.value ? cart.hasItem(currentPlan.value.id, cartCycle.value) : false))

/**
 * Adds the current plan to the cart.
 *
 * The selected cycle is used when it is priced, otherwise the monthly one, and a free plan (zero monthly
 * price, as in the catalogue store) is always added at no charge on the monthly cycle, labelled free.
 */
const addToCart = (): void => {
  const plan = currentPlan.value
  if (!plan || !currencyStore.payerCode) {
    return
  }

  const free = isFree.value
  const billingCycle = cartCycle.value
  const amount = free ? 0 : (cycleAmount(plan.pricing, billingCycle) ?? cycleAmount(plan.pricing, PricingCycle.Monthly))
  if (amount === null) {
    return
  }

  cart.addItem({
    pid: plan.id,
    name: plan.name,
    billingcycle: billingCycle,
    cycleLabel: free
      ? t('hosting.cycles.free')
      : t(`hosting.cycles.${PRICING_CYCLE_I18N_KEY[billingCycle]}`),
    amount,
    currency: currencyStore.payerCode,
  })
}

useSeoMeta({
  title: () => currentPlan.value ? t('seo.hostingPlan.named', { name: currentPlan.value.name, site: siteName.value }) : t('seo.hostingPlan.title'),
  description: () => planSummary.value || t('seo.hostingPlan.description'),
  ogTitle: () => currentPlan.value ? t('seo.hostingPlan.named', { name: currentPlan.value.name, site: siteName.value }) : t('seo.hostingPlan.title'),
  ogDescription: () => planSummary.value || t('seo.hostingPlan.description'),
  ogType: 'website',
  twitterCard: 'summary_large_image',
  twitterTitle: () => currentPlan.value ? `${currentPlan.value.name} | ${siteName.value}` : t('seo.hostingPlan.title'),
  twitterDescription: () => planSummary.value || t('seo.hostingPlan.description'),
})

/** Public site address the canonical links are built from. */
const baseUrl = useRuntimeConfig().public.baseUrl

useHead(() => ({
  link: buildSeoLinks(baseUrl, `/hosting/${planParam.value}`, locale.value),
  meta: [{ name: 'keywords', content: t('seo.hostingPlan.keywords') }],
}))
</script>
