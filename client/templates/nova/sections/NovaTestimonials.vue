<template>
  <UiSection v-if="testimonials.length" padding="lg" class="border-b border-nova-border bg-nova-surface">
    <NovaSectionHeader :eyebrow="t('nova.testimonials.eyebrow')" :title="t('nova.testimonials.title')" />

    <!-- Compact cards at up to three per row; fewer quotes than columns stay centred at the normal width. -->
    <UiStack :as="UiStackTag.Ul" direction="row" wrap justify="center" gap="md" class="mt-8">
      <UiStack v-for="testimonial in testimonials" :key="testimonial.id" :as="UiStackTag.Li" gap="none" class="w-full sm:w-[calc((100%-1.25rem)/2)] lg:w-[calc((100%-2.5rem)/3)]">
        <UiCard padding="md" class="flex h-full flex-col !border-nova-border !bg-nova-bg">
          <UiStack :as="UiStackTag.Figure" gap="none" class="h-full">
            <Icon name="lucide:quote" class="h-4 w-4 text-nova-brand" aria-hidden="true" />
            <UiText :as="UiTextTag.Blockquote" size="inherit" tone="inherit" class="mt-3 flex-1 text-sm leading-relaxed text-nova-ink">
              {{ testimonial.quote }}
            </UiText>
            <UiStack :as="UiStackTag.Figcaption" gap="none" class="mt-4 border-t border-nova-border pt-3 text-sm">
              <UiText :as="UiTextTag.Span" size="inherit" tone="inherit" weight="semibold" class="text-nova-ink">{{ testimonial.name }}</UiText>
              <UiText v-if="attribution(testimonial)" :as="UiTextTag.Span" size="inherit" tone="inherit" class="block text-xs text-nova-muted">{{ attribution(testimonial) }}</UiText>
            </UiStack>
          </UiStack>
        </UiCard>
      </UiStack>
    </UiStack>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * nova testimonials. The quotes come from the storefront store
 * (`GET /api/public/testimonials`); with none the section renders nothing, not a placeholder,
 * and certainly not an invented name.
 */
import NovaSectionHeader from '~/templates/nova/sections/NovaSectionHeader.vue'
import { useStorefrontStore } from '~/stores/storefront'
import type { Testimonial } from '~/types/testimonial'
import { UiStackTag, UiTextTag } from '~/utils/ui/enums'

/** Translator for the copy, and the active language. */
const { t, locale } = useI18n()

/** The storefront content: testimonials, partners, projects and FAQs. */
const storefront = useStorefrontStore()

// Not awaited: Nuxt still holds the server render for it, and the section stays synchronous.
useAsyncData('storefront:testimonials', () => storefront.loadTestimonials(), { watch: [locale] })

/** The quotes, in display order. */
const testimonials = computed(() => storefront.testimonials)

/**
 * Joins a quote's role and company for the line under the name.
 *
 * @param testimonial The quote.
 * @returns The role and company, comma-separated; empty when it has neither.
 */
const attribution = (testimonial: Testimonial): string =>
  [testimonial.role, testimonial.company].filter(Boolean).join(', ')
</script>
