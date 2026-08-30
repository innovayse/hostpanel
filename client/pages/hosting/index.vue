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
const { t } = useI18n()
const { slot } = useTemplate()

const hosting = slot('hosting')

// SEO — this page shipped without any, so it had no title, canonical or
// hreflang. The seo.hosting copy already existed in all three locales.
useSeo({
  title: t('seo.hosting.title'),
  description: t('seo.hosting.description'),
  keywords: t('seo.hosting.keywords'),
  type: 'website',
  path: '/hosting'
})

const { plans } = usePortalPlans()
const yearly = ref(false)

// The comparison rows are assembled here rather than in the template: which
// column a value belongs to is data, and templates only render what they are
// handed.
const { features } = useProductFeatures(HOSTING_GROUP_ID)
const comparisonRows = computed(() =>
  buildComparisonRows(features.value, plans.value.map(plan => plan.id)))
</script>
