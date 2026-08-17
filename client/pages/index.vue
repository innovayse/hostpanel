<template>
  <component
    :is="home"
    :domain-results="domainResults"
    :domain-pending="domainPending"
    :has-zones="offeredTlds.length > 0"
    :price-hints="offeredTlds"
    :plans="plans"
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
</script>
