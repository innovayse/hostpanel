<template>
  <UiStack gap="none" data-testid="classic-home">
    <!-- H1 for SEO: visually hidden, semantically the first heading on the page -->
    <UiHeading :level="UiHeadingLevel.H1" class="sr-only" data-testid="classic-home-title">{{ $t('seo.home.h1') }}</UiHeading>

    <!-- Hero: products full-screen slider (not toggleable) -->
    <SectionsProductsFullSlider />

    <!-- Performance figures -->
    <SectionsPerformance v-if="isVisible(HomeSection.Performance)" />

    <!-- Plans / products -->
    <SectionsProducts v-if="isVisible(HomeSection.Plans)" />

    <!-- Why choose us / services -->
    <SectionsWhyChooseUs v-if="isVisible(HomeSection.Services)" />

    <!-- Server locations -->
    <SectionsLocations v-if="isVisible(HomeSection.Locations)" />

    <!-- Security -->
    <SectionsSecurity v-if="isVisible(HomeSection.Security)" />

    <!-- One-click apps -->
    <SectionsApps v-if="isVisible(HomeSection.Apps)" />

    <!-- Guarantees -->
    <SectionsGuarantees v-if="isVisible(HomeSection.Guarantees)" />

    <!-- Process -->
    <SectionsProcess v-if="isVisible(HomeSection.Process)" />

    <!-- Testimonials -->
    <SectionsTestimonials v-if="isVisible(HomeSection.Testimonials)" />

    <!-- Support channels -->
    <SectionsSupport v-if="isVisible(HomeSection.Support)" />

    <!-- Partners / clients logos: one section for both the Partners and the Trusted by switches -->
    <SectionsPartners v-if="isPartnersVisible(isVisible)" />

    <!-- FAQ -->
    <SectionsFAQ v-if="isVisible(HomeSection.Faq)" :limit="HOME_FAQ_LIMIT" :show-categories="false" />

    <!-- Call to action -->
    <SectionsCTA v-if="isVisible(HomeSection.Cta)" />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * ClassicHome. The classic template's homepage: a hidden H1 followed by the marketing sections
 * in hosting-site order. Every section except the hero is switched on or off by its
 * `portal.sections.*` setting through {@link useHomeSections}; the classic template has no
 * projects section, so that toggle has nothing to hide here.
 */
import { HOME_FAQ_LIMIT } from '~/constants/faq'
import { HomeSection } from '~/constants/homeSection'
import { isPartnersVisible } from '~/utils/partnersVisible'
import { UiHeadingLevel } from '~/utils/ui/enums'

/** Whether a home section is switched on. */
const { isVisible } = useHomeSections()
</script>
