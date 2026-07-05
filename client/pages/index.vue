<template>
  <div>
    <!-- H1 for SEO — visually hidden, semantically first heading on the page -->
    <h1 class="sr-only">{{ $t('seo.home.h1') }}</h1>

    <!-- Products Full-Screen Slider -->
    <SectionsProductsFullSlider />

    <!-- Partners/Clients logos -->
    <SectionsPartners />

    <!-- Why Choose Us Section -->
    <SectionsWhyChooseUs />

    <!-- Process Section -->
    <SectionsProcess />

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
</script>
