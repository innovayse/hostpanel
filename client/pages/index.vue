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
    @update:yearly="value => (yearly = value)"
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
const { t } = useI18n()
const { slot } = useTemplate()

const home = slot('home')

// SEO setup with canonical, hreflang, OG, Twitter tags
useSeo({
  title: t('seo.home.title'),
  description: t('seo.home.description'),
  keywords: t('seo.home.keywords'),
  type: 'website',
  path: '/'
})

// Schema.org structured data
const { organizationSchema, localBusinessSchema, websiteSchema, injectSchema } = useSchemaOrg()

// Inject organization and website schemas
injectSchema([
  organizationSchema(),
  localBusinessSchema(),
  websiteSchema()
])

const { results: domainResults, pending: domainPending, search, offeredTlds } = useDomainLookup()
const { plans } = usePortalPlans()

const yearly = ref(false)

// Assembled here rather than in the template, for the same reason
// pages/hosting/index.vue assembles it: which column a value belongs to is
// data, and templates only render what they are handed. aurora's home slot
// declares no such prop and simply ignores it.
const { features } = useProductFeatures(HOSTING_GROUP_ID)
const comparisonRows = computed(() =>
  buildComparisonRows(features.value, plans.value.map(plan => plan.id)))
</script>
