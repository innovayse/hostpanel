<template>
  <UiSection v-if="testimonials.length" :contained="false" padding="lg" class="bg-classic-surface border-y border-classic-border" data-testid="sections-testimonials">
    <UiContainer>
      <ClassicSectionHeader
        class="mb-10"
        :eyebrow="$t('testimonials.badge')"
        :title="$t('testimonials.title')"
        :subtitle="$t('testimonials.subtitle')"
      />

      <!-- Carousel -->
      <UiStack gap="none" class="relative">
        <ClientOnly>
          <Swiper
            :modules="modules"
            :slides-per-view="1"
            :space-between="30"
            :loop="testimonials.length > 1"
            :autoplay="{ delay: TESTIMONIAL_AUTOPLAY_MS, disableOnInteraction: false, pauseOnMouseEnter: true }"
            class="pb-4"
            @swiper="onSwiper"
            @slide-change="onSlideChange"
          >
            <SwiperSlide v-for="testimonial in testimonials" :key="testimonial.id">
              <ClassicCard :data-testid="`testimonial-${testimonial.id}`">
                <UiStack gap="md">
                  <!-- Rating -->
                  <UiStack direction="row" gap="none" class="gap-1">
                    <Star
                      v-for="star in testimonial.rating"
                      :key="star"
                      :size="16"
                      :stroke-width="2"
                      fill="currentColor"
                      class="text-classic-brand"
                    />
                  </UiStack>

                  <UiText tone="inherit" class="text-base leading-relaxed text-classic-ink">"{{ testimonial.quote }}"</UiText>

                  <!-- Author -->
                  <UiStack direction="row" align="center" gap="sm" class="border-t border-classic-border pt-4">
                    <UiStack align="center" justify="center" gap="none" class="h-10 w-10 shrink-0 rounded-full bg-classic-brand-soft text-xs font-bold text-classic-brand">
                      {{ getInitials(testimonial.name) }}
                    </UiStack>
                    <UiStack gap="none">
                      <UiText tone="inherit" weight="semibold" class="text-classic-ink">{{ testimonial.name }}</UiText>
                      <UiText v-if="testimonial.role" tone="inherit" size="xs" class="text-classic-muted">{{ testimonial.role }}</UiText>
                      <UiText v-if="testimonial.company" tone="inherit" size="xs" class="text-classic-muted">{{ testimonial.company }}</UiText>
                    </UiStack>
                  </UiStack>
                </UiStack>
              </ClassicCard>
            </SwiperSlide>
          </Swiper>
          <UiStack v-if="testimonials.length > 1" align="center" gap="none" class="mt-4">
            <UiCarouselDots :count="testimonials.length" :active="active" @select="select" />
          </UiStack>
          <!-- The carousel only exists in the browser; the server render carries every quote as plain text. -->
          <template #fallback>
            <UiStack :as="UiStackTag.Ul" gap="none" class="sr-only" data-testid="testimonials-fallback">
              <UiListItem v-for="testimonial in testimonials" :key="testimonial.id">
                <UiText :as="UiTextTag.Span">"{{ testimonial.quote }}" - {{ testimonial.name }}</UiText>
              </UiListItem>
            </UiStack>
          </template>
        </ClientOnly>

        <UiText size="sm" tone="inherit" class="text-center mt-6 text-classic-muted">{{ $t('testimonials.hint') }}</UiText>
      </UiStack>
    </UiContainer>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * Testimonials carousel (Swiper).
 *
 * The quotes come from the storefront store (`GET /api/portal/public/testimonials`, localised by the API);
 * with none the section renders nothing. Each quote sits on a flat classic card with brand-coloured
 * stars and an initials badge, in light and dark mode alike.
 */
import { Star } from 'lucide-vue-next'
import { Swiper, SwiperSlide } from 'swiper/vue'
import { Autoplay } from 'swiper/modules'
import { TESTIMONIAL_AUTOPLAY_MS } from '~/constants/uiDefaults'
import 'swiper/css'
import { useCarouselDots } from '~/composables/useCarouselDots'
import { useStorefrontStore } from '~/stores/storefront'
import { getInitials } from '~/utils/design'
import { UiStackTag, UiTextTag } from '~/utils/ui/enums'

/** Swiper modules the carousel uses. */
const modules = [Autoplay]

/** The dots' position and handlers, driven from the carousel. */
const { active, onSwiper, onSlideChange, select } = useCarouselDots()

/** The active language; the quotes are re-read when it changes. */
const { locale } = useI18n()

/** The storefront content: testimonials, partners, projects and FAQs. */
const storefront = useStorefrontStore()

// Not awaited: Nuxt still holds the server render for it, and the section stays synchronous.
useAsyncData('storefront:testimonials', () => storefront.loadTestimonials(), { watch: [locale] })

/** The testimonials to show, in display order. */
const testimonials = computed(() => storefront.testimonials)
</script>
