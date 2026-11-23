<template>
  <UiStack v-if="items.length" :as="UiStackTag.Section" gap="none" class="py-8 md:py-20 bg-ui-page relative" data-testid="products-testimonials">
    <UiContainer class="w-full">
      <MarketingSectionHeader class="mb-12" :subtitle="$t('products.testimonials.subtitle')">
        <template #title>
          <i18n-t keypath="products.testimonials.title" tag="span">
            <template #thousands>
              <UiText :as="UiTextTag.Span" size="inherit" tone="inherit" weight="bold" class="text-secondary-700 dark:text-secondary-400 [.tpl-classic_&]:text-classic-brand">{{ $t('products.testimonials.thousandsHighlight') }}</UiText>
            </template>
          </i18n-t>
        </template>
      </MarketingSectionHeader>

      <UiGrid cols="1" gap="lg" class="md:grid-cols-3">
        <UiCard
          v-for="(testimonial, index) in items"
          :key="testimonial.id"
          class="group hover:border-secondary-500/30 hover:-translate-y-2 transition-all duration-300 animate-reveal-up motion-reduce:animate-none [.tpl-classic_&]:hover:border-classic-border [.tpl-classic_&]:hover:translate-y-0"
          :data-testid="`product-testimonial-${testimonial.id}`"
          :class="revealDelay(index * 100)"
        >
          <UiStack direction="row" gap="none" class="gap-1 mb-4">
            <Star v-for="star in testimonial.rating" :key="star" :size="14" :stroke-width="2" fill="currentColor" class="text-secondary-700 dark:text-secondary-400 [.tpl-classic_&]:text-classic-brand" />
          </UiStack>

          <UiText size="sm" class="mb-4 italic leading-relaxed text-gray-700 dark:text-gray-300 [.tpl-classic_&]:not-italic [.tpl-classic_&]:text-classic-ink">"{{ testimonial.text }}"</UiText>

          <UiStack direction="row" align="center" gap="sm">
            <UiAvatar :first-name="testimonial.name" />
            <UiStack gap="none">
              <UiText size="sm" weight="semibold">{{ testimonial.name }}</UiText>
              <UiText v-if="testimonial.company" size="xs" tone="subtle">{{ testimonial.company }}</UiText>
            </UiStack>
          </UiStack>
        </UiCard>
      </UiGrid>
    </UiContainer>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Three client testimonials for the products page, each quote cut to 120 characters. They come
 * from the storefront store; with none the section renders nothing.
 */

import { revealDelay } from '~/utils/reveal'
import { Star } from 'lucide-vue-next'
import { useStorefrontStore } from '~/stores/storefront'
import { UiStackTag, UiTextTag } from '~/utils/ui/enums'

/** Longest quote shown before it is cut with an ellipsis. */
const QUOTE_LIMIT = 120

/** The active language; the quotes are re-read when it changes. */
const { locale } = useI18n()

/** The storefront content: testimonials, partners, projects and FAQs. */
const storefront = useStorefrontStore()

// Not awaited: Nuxt still holds the server render for it, and the section stays synchronous.
useAsyncData('storefront:testimonials', () => storefront.loadTestimonials(), { watch: [locale] })

/** The first three testimonials with their shortened text. */
const items = computed(() => storefront.testimonials.slice(0, 3).map((testimonial) => ({
  id: testimonial.id,
  name: testimonial.name,
  company: testimonial.company,
  rating: testimonial.rating,
  text: testimonial.quote.length > QUOTE_LIMIT ? `${testimonial.quote.slice(0, QUOTE_LIMIT)}...` : testimonial.quote,
})))
</script>
