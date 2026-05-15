<template>
  <div>
    <!-- H1 for SEO — visually hidden, semantically first heading on the page -->
    <h1 class="sr-only">{{ $t('seo.home.h1') }}</h1>

    <!-- Products Full-Screen Slider -->
    <SectionsProductsFullSlider />

    <!-- Partners/Clients logos -->
    <SectionsPartners />

    <!-- Services Section -->
    <SectionsServices />

    <!-- Why Choose Us Section -->
    <SectionsWhyChooseUs />

    <!-- Process Section -->
    <SectionsProcess />

    <!-- Portfolio Section -->
    <SectionsPortfolio />

    <!-- Testimonials Section -->
    <SectionsTestimonials />

    <!-- Products Section -->
    <SectionsProducts />

    <!-- FAQ Section -->
    <SectionsFAQ :limit="6" :show-categories="false" />

    <!-- CTA Section -->
    <SectionsCTA />
  </div>
</template>

<script setup lang="ts">
/**
 * Home page with all landing sections
 */

const { t } = useI18n()

// SEO setup with canonical, hreflang, OG, Twitter tags
const { baseUrl } = useSeo({
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

// Service schemas for SEO
const serviceSchemas = [
  {
    '@context': 'https://schema.org',
    '@type': 'Service',
    name: 'Web Development',
    description: 'Custom web development services including corporate websites, e-commerce, web applications, and CMS solutions',
    url: `${baseUrl}/services#development`,
    serviceType: 'Web Development',
    provider: {
      '@id': `${baseUrl}/#organization`
    }
  },
  {
    '@context': 'https://schema.org',
    '@type': 'Service',
    name: 'SEO Services',
    description: 'Comprehensive SEO services including technical SEO, content optimization, link building, and local SEO',
    url: `${baseUrl}/services#seo`,
    serviceType: 'Search Engine Optimization',
    provider: {
      '@id': `${baseUrl}/#organization`
    }
  },
  {
    '@context': 'https://schema.org',
    '@type': 'Service',
    name: 'PPC Advertising',
    description: 'Pay-per-click advertising management for Google Ads and Yandex Direct campaigns',
    url: `${baseUrl}/services#ppc`,
    serviceType: 'PPC Management',
    provider: {
      '@id': `${baseUrl}/#organization`
    }
  }
]

useHead({
  script: serviceSchemas.map(schema => ({
    type: 'application/ld+json',
    children: JSON.stringify(schema)
  }))
})
</script>
