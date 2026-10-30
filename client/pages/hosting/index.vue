<template>
  <component
    :is="hosting"
    :plans="plans"
    :yearly="yearly"
    :comparison-rows="comparisonRows"
    @update:yearly="(value: boolean) => (yearly = value)"
  />
</template>

<script setup lang="ts">
/**
 * Hosting plans page.
 *
 * SEO belongs here so both templates emit the same head. The `classic` template
 * component is a lift-and-shift of this page's original body and loads its own
 * data; `aurora` takes the plans below as props. The duplicate request that
 * implies is absorbed by the products endpoint's server-side cache, and it buys
 * a far smaller change to a 500-line page that already works.
 */
import { HOSTING_GROUP_ID } from '~/constants/catalogGroup'
import { usePayerAsyncData } from '~/composables/usePayerAsyncData'
import { useCatalogStore } from '~/stores/catalog'
import { useCurrencyStore } from '~/stores/currency'
import { SeoPageType } from '~/types/seopagetype'
import { TemplateSlot } from '~/types/templateslot'

/** Translation helpers and the active language. */
const { t, locale } = useI18n()

/** Operator-set site name that the SEO copy interpolates as `{site}`. */
const { name: siteName } = useSiteIdentity()
/** Prefixes a path with the active language. */
const localePath = useLocalePath()
/** The template registry: resolves the component the active template provides for a slot. */
const { slot } = useTemplate()

/** The template's hosting component (classic or aurora), resolved by the registry. */
const hosting = slot(TemplateSlot.Hosting)

// SEO: title, canonical and hreflang from the seo.hosting copy in all three locales.
useSeo({
  title: t('seo.hosting.title', { site: siteName.value }),
  description: t('seo.hosting.description', { site: siteName.value }),
  keywords: t('seo.hosting.keywords', { site: siteName.value }),
  type: SeoPageType.Website,
  path: '/hosting'
})

/** The payer's currency and the currencies prices can be shown in. */
const currencyStore = useCurrencyStore()
/** The catalogue: products, plans, slides and TLD prices. */
const catalogStore = useCatalogStore()

// The currencies load on the server too: without them the payer's currency (and its symbol)
// is unknown during the server render and the plan prices print as bare numbers.
await useAsyncData('hosting:currencies', () => currencyStore.load())

// Prices follow the payer's currency, so the plans are read again when it resolves or changes.
await Promise.all([
  usePayerAsyncData('hosting:plans', () => catalogStore.loadHostingPlans(), {
    watch: [locale]
  }),
  useAsyncData('hosting:features', () => catalogStore.loadProductFeatures(HOSTING_GROUP_ID), {
    watch: [locale]
  })
])

/** The hosting plans as the storefront cards, each linking to its configure page. */
const plans = computed(() => catalogStore.planCards(id => localePath(`/configure/${id}`), catalogStore.featuresFor(HOSTING_GROUP_ID)))
/** Whether the visitor chose yearly billing; the template reports changes through `update:yearly`. */
const yearly = ref(false)

onMounted(() => {
  currencyStore.init()
})

/**
 * The comparison rows, assembled here rather than in the template: which
 * column a value belongs to is data, and templates only render what they are
 * handed.
 */
const comparisonRows = computed(() =>
  buildComparisonRows(catalogStore.featuresFor(HOSTING_GROUP_ID), plans.value.map(plan => plan.id)))
</script>
