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
/** classic template homepage — the original landing composition. */
</script>
