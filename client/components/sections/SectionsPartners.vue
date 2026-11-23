<template>
  <UiSection v-if="partners.length" :contained="false" padding="none" class="py-10 sm:py-16 bg-ui-page relative overflow-hidden" data-testid="sections-partners">
    <!-- Background -->
    <UiDecor class="inset-0">
      <UiDecor class="inset-0 bg-gradient-to-b from-transparent via-white/[0.02] to-transparent" />
      <UiDecor class="top-10 left-10 w-2 h-2 bg-primary-400/40 rounded-full animate-float" />
      <UiDecor class="top-20 right-20 w-3 h-3 bg-secondary-600/30 dark:bg-secondary-400/30 rounded-full animate-float [animation-delay:1s]" />
      <UiDecor class="bottom-10 left-1/3 w-2 h-2 bg-cyan-400/40 rounded-full animate-float [animation-delay:2s]" />
    </UiDecor>

    <UiContainer class="relative z-10">
      <!-- Header -->
      <UiStack gap="none" class="text-center mb-8 sm:mb-12 animate-reveal-up motion-reduce:animate-none">
        <UiText size="sm" tone="subtle" class="uppercase tracking-wider mb-2">{{ $t('clients.subtitle') }}</UiText>
        <UiHeading :level="3" size="subsection" class="text-xl">{{ $t('clients.heading') }}</UiHeading>
      </UiStack>

      <!-- A short list sits centred and wrapped; a marquee of two names reads as a half-empty strip. -->
      <UiLogoWall
        :items="partners"
        testid-prefix="partner"
        :layout="marquee ? UiLogoWallLayout.Marquee : UiLogoWallLayout.Grid"
        class="text-gray-500 dark:text-gray-400"
        :data-testid="marquee ? 'sections-partners-marquee' : 'sections-partners-static'"
      />
    </UiContainer>

    <!-- Decorative corner accents -->
    <UiDecor class="top-0 right-0 w-32 h-32 border-r-2 border-t-2 border-secondary-500/25" />
    <UiDecor class="bottom-0 left-0 w-32 h-32 border-l-2 border-b-2 border-primary-500/25" />
  </UiSection>
</template>

<script setup lang="ts">
/**
 * Partners section: partner logos in a balanced grid, or a marquee when there are many (a plain wordmark for a partner without a logo). The partners come from the storefront
 * store (`GET /api/portal/public/partners`, localised by the API); with none the section renders
 * nothing.
 */
import { useStorefrontStore } from '~/stores/storefront'
import { UiLogoWallLayout } from '~/utils/ui/logoWall'

/** The active language; the partners are re-read when it changes. */
const { locale } = useI18n()

/** The storefront content: testimonials, partners, projects and FAQs. */
const storefront = useStorefrontStore()

// Not awaited: Nuxt still holds the server render for it, and the section stays synchronous.
useAsyncData('storefront:partners', () => storefront.loadPartners(), { watch: [locale] })

/** The partners to show, in display order. */
const partners = computed(() => storefront.partners)

/** Fewest partners that fill a scrolling strip; below it the logos sit centred instead. */
const MARQUEE_MIN_PARTNERS = 6

/** Whether there are enough partners to run the marquee. */
const marquee = computed(() => partners.value.length >= MARQUEE_MIN_PARTNERS)
</script>
