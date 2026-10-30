<template>
  <component
    :is="home"
    :domain-results="domainResults"
    :domain-pending="domainPending"
    :has-zones="offeredTlds.length > 0"
    :price-hints="offeredTlds"
    :plans="plans"
    :comparison-rows="comparisonRows"
    :yearly="yearly"
    @search="search"
    @update:yearly="setYearly"
  />
</template>

<script setup lang="ts">
/**
 * Home page.
 *
 * The page owns SEO, structured data and every network call; the active
 * template owns the markup. Both templates render the same canonical, hreflang
 * and schema.org output, which is why those calls live here and never inside a
 * template.
 */
import { HOSTING_GROUP_ID } from '~/constants/catalogGroup'
import { usePayerAsyncData } from '~/composables/usePayerAsyncData'
import { useCatalogStore } from '~/stores/catalog'
import { useCurrencyStore } from '~/stores/currency'
import { useDomainsStore } from '~/stores/domains'
import { SeoPageType } from '~/types/seopagetype'
import { TemplateSlot } from '~/types/templateslot'

/** Translates a key in the active language; `locale` is that language. */
const { t, locale } = useI18n()

/** Operator-set site name that the SEO copy interpolates as `{site}`. */
const { name: siteName } = useSiteIdentity()

/** Localised path builder from the i18n module. */
const localePath = useLocalePath()
/** Resolves the component the active template provides for a named slot. */
const { slot } = useTemplate()

/** The active template's home component. */
const home = slot(TemplateSlot.Home)

// SEO setup with canonical, hreflang, OG, Twitter tags
useSeo({
  title: t('seo.home.title', { site: siteName.value }),
  description: t('seo.home.description', { site: siteName.value }),
  keywords: t('seo.home.keywords', { site: siteName.value }),
  type: SeoPageType.Website,
  path: '/'
})

// Schema.org structured data
/** Builders and the publisher of the schema.org structured data. */
const { organizationSchema, localBusinessSchema, websiteSchema, injectSchema } = useSchemaOrg()

// Inject organization and website schemas
injectSchema([
  organizationSchema(),
  localBusinessSchema(),
  websiteSchema()
])

/** The currency store: the payer's currency. */
const currencyStore = useCurrencyStore()
/** The catalog store: plans, features and TLD prices. */
const catalogStore = useCatalogStore()
/** The domains store: the domain lookup. */
const domainsStore = useDomainsStore()

// The currencies load on the server too: without them the payer's currency (and its symbol)
// is unknown during the server render and the plan prices print as bare numbers.
await useAsyncData('home:currencies', () => currencyStore.load())

// Prices follow the payer's currency, so each read runs again when it resolves or changes.
await Promise.all([
  usePayerAsyncData('home:tld-pricing', () => catalogStore.loadTldPricing(), {
    watch: [locale]
  }),
  usePayerAsyncData('home:hosting-plans', () => catalogStore.loadHostingPlans(), {
    watch: [locale]
  }),
  useAsyncData('home:hosting-features', () => catalogStore.loadProductFeatures(HOSTING_GROUP_ID), {
    watch: [locale]
  })
])

/** The domain lookup state the template's search box renders. */
const { lookupResults: domainResults, lookupPending: domainPending, offeredTlds } = storeToRefs(domainsStore)

/** Starts a domain search across the extensions the operator offers. */
const search = domainsStore.searchOfferedTlds

/** The hosting plans as cards, each linking to its configure page. */
const plans = computed(() => catalogStore.planCards(id => localePath(`/configure/${id}`), catalogStore.featuresFor(HOSTING_GROUP_ID)))

onMounted(() => {
  currencyStore.init()
})

/** Whether the plan prices are shown per year instead of per month. */
const yearly = ref(false)

/**
 * Stores the billing period the visitor picked in the template.
 *
 * @param value - True for yearly prices.
 */
const setYearly = (value: boolean): void => {
  yearly.value = value
}

// Assembled here rather than in the template, for the same reason
// pages/hosting/index.vue assembles it: which column a value belongs to is
// data, and templates only render what they are handed. aurora's home slot
// declares no such prop and simply ignores it.
/** The feature comparison table: one row per feature, one column per plan. */
const comparisonRows = computed(() =>
  buildComparisonRows(catalogStore.featuresFor(HOSTING_GROUP_ID), plans.value.map(plan => plan.id)))
</script>
