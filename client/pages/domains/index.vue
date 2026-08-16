<template>
  <component
    :is="domains"
    :price-rows="priceRows"
    :results="results"
    :pending="pending"
    @search="search"
  />
</template>

<script setup lang="ts">
/**
 * Domains page.
 *
 * SEO and structured data belong here so both templates emit the same head. The
 * `classic` template component is a lift-and-shift of this page's original body
 * and loads its own data; `aurora` takes the pricing and lookup results below as
 * props.
 */
const { t } = useI18n()
const { slot } = useTemplate()

const domains = slot('domains')

// SEO
const { baseUrl } = useSeo({
  title: t('seo.domains.title'),
  description: t('seo.domains.description'),
  keywords: t('seo.domains.keywords'),
  type: 'website',
  path: '/domains'
})

// Schema.org
const { organizationSchema, injectSchema } = useSchemaOrg()
injectSchema([
  organizationSchema(),
  {
    '@context': 'https://schema.org',
    '@type': 'CollectionPage',
    '@id': `${baseUrl}/domains#domainspage`,
    url: `${baseUrl}/domains`,
    name: t('seo.domains.title'),
    description: t('seo.domains.description'),
    inLanguage: ['en', 'ru', 'hy'],
    publisher: { '@id': `${baseUrl}/#organization` }
  }
])

const { priceRows, results, pending, search } = useDomainLookup()
</script>
